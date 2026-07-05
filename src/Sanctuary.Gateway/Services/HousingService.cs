using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Services;

public static class HousingService
{
    private const ulong SyntheticHouseGuidBase = 0x4800000000000000;
    private const ulong SyntheticFixtureGuidBase = 0x4900000000000000;
    private const int SyntheticBuildItemGuidBase = 1_800_000_000;
    private const int MaxFixtureCount = 50000;

    private static readonly HashSet<string> SafeBuildBlockModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "hsg_block_01.adr",
        "hsg_block_02.adr",
        "hsg_block_03.adr",
        "hsg_block_flatrectangle_01.adr",
        "hsg_block_flatsquare_01.adr",
        "hsg_block_ramp_01.adr",
        "hsg_block_wedge_01.adr",
        "hsg_block_cylinder_01.adr",
        "hsg_block_cylinderhalf_01.adr",
        "hsg_block_cylinderhalf_02.adr",
        "hsg_block_arch_01.adr"
    };

    private static readonly ConcurrentDictionary<ulong, PlayerHousingInstanceData> Instances = new();
    private static readonly ConcurrentDictionary<ulong, int> FixtureDefinitionByGuid = new();

    public static bool SendHousingUi(GatewayConnection connection, IResourceManager resourceManager, ILogger logger)
    {
        var definitions = GetFixtureDefinitions(resourceManager);

        var sentItems = SendBuildItems(connection, resourceManager, definitions.Take(6));

        connection.SendTunneled(new ExecuteScriptPacket
        {
            Script = "MyStuffBrowser.Show"
        });

        logger.LogInformation(
            "Requested free-build My Stuff browser. PlayerGuid={playerGuid}, BuildItemsSent={buildItemsSent}",
            connection.Player.Guid,
            sentItems);

        return true;
    }

    private static int SendBuildItems(
        GatewayConnection connection,
        IResourceManager resourceManager,
        IEnumerable<FixtureDefinition> definitions)
    {
        var sentItems = 0;

        foreach (var fixtureDefinition in definitions)
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(
                    fixtureDefinition.ItemDefinitionId,
                    out var itemDefinition))
            {
                continue;
            }

            using var writer = new PacketWriter();
            itemDefinition.Serialize(writer);

            connection.SendTunneled(new PlayerUpdatePacketItemDefinitions
            {
                Payload = writer.Buffer
            });

            var itemGuid = MakeSyntheticBuildItemGuid(fixtureDefinition.ItemDefinitionId);
            var clientItem = connection.Player.Items.FirstOrDefault(x => x.Id == itemGuid);
            if (clientItem is null)
            {
                clientItem = new ClientItem
                {
                    Id = itemGuid,
                    Definition = fixtureDefinition.ItemDefinitionId,
                    Tint = 0,
                    Count = 999,
                    ConsumedCount = 0,
                    LastCastTime = int.MaxValue,
                    AbilityCount = 0,
                    ActivateEnabled = false
                };

                connection.Player.Items.Add(clientItem);
            }

            connection.SendTunneled(ItemActionBarService.CreateItemAdd(clientItem, itemDefinition));
            sentItems++;
        }

        return sentItems;
    }

    public static bool SendFixtureItemList(GatewayConnection connection, IResourceManager resourceManager)
    {
        var definitions = GetFixtureDefinitions(resourceManager);
        var infos = definitions
            .Select(x => new FixtureInstanceInfo
            {
                FixtureGuid = MakeSyntheticFixtureGuid(x.ItemDefinitionId),
                ItemDefinitionId = x.ItemDefinitionId,
                Unknown3 = 1,
                Unknown4 = 1,
                Unknown5 = 1
            })
            .ToList();

        foreach (var definition in definitions)
            FixtureDefinitionByGuid[MakeSyntheticFixtureGuid(definition.ItemDefinitionId)] = definition.ItemDefinitionId;

        connection.SendTunneled(new HousingPacketFixtureItemList
        {
            Infos = infos,
            Definitions = definitions,
            Effects = []
        });

        return true;
    }

    public static bool PlaceFixture(
        GatewayConnection connection,
        IResourceManager resourceManager,
        int requestedItemDefinitionId,
        ulong requestedFixtureGuid,
        Vector4 requestedPosition,
        Quaternion requestedRotation,
        ILogger logger)
    {
        var itemDefinitionId = ResolveItemDefinitionId(resourceManager, requestedItemDefinitionId, requestedFixtureGuid);
        if (itemDefinitionId == 0)
        {
            logger.LogWarning(
                "Accepted housing placement request but could not resolve a block definition. PlayerGuid={playerGuid}, RequestedDefinition={definition}, RequestedGuid={fixtureGuid}",
                connection.Player.Guid,
                requestedItemDefinitionId,
                requestedFixtureGuid);
            SendFixtureItemList(connection, resourceManager);
            return true;
        }

        var instance = GetOrCreateInstance(connection);
        var fixtureGuid = requestedFixtureGuid == 0 || FixtureDefinitionByGuid.ContainsKey(requestedFixtureGuid)
            ? MakePlacedFixtureGuid(connection, instance.Fixtures.Count + 1)
            : requestedFixtureGuid;

        var position = IsUsablePosition(requestedPosition)
            ? requestedPosition
            : connection.Player.Position;

        if (position.W == 0.0f)
            position.W = 1.0f;

        var rotation = IsUsableRotation(requestedRotation)
            ? requestedRotation
            : connection.Player.Rotation;

        var fixture = CreateFixture(fixtureGuid, instance.HouseGuid, itemDefinitionId, position, rotation);
        instance.Fixtures[(uint)fixtureGuid] = fixture;
        instance.CurFixtureCount = instance.Fixtures.Count;

        connection.SendTunneled(new HousingPacketFixtureUpdate
        {
            Fixture = fixture
        });

        SendHouseInfo(connection, true);

        logger.LogInformation(
            "Placed free-build housing fixture. PlayerGuid={playerGuid}, ItemDefinitionId={definition}, FixtureGuid={fixtureGuid}, Position={position}",
            connection.Player.Guid,
            itemDefinitionId,
            fixtureGuid,
            position);

        return true;
    }

    public static bool SaveFixture(GatewayConnection connection, ulong fixtureGuid, Vector4 position, Quaternion rotation)
    {
        var instance = GetOrCreateInstance(connection);

        var fixture = instance.Fixtures.Values.FirstOrDefault(x => x.Guid == fixtureGuid);
        if (fixture is null)
            return true;

        if (IsUsablePosition(position))
            fixture.Unknown5 = position;

        if (IsUsableRotation(rotation))
            fixture.Unknown6 = rotation;

        connection.SendTunneled(new HousingPacketFixtureUpdate
        {
            Fixture = fixture
        });

        return true;
    }

    public static bool RemoveFixture(GatewayConnection connection, ulong fixtureGuid)
    {
        var instance = GetOrCreateInstance(connection);
        var key = instance.Fixtures.FirstOrDefault(x => x.Value.Guid == fixtureGuid).Key;

        if (key != 0)
            instance.Fixtures.Remove(key);

        instance.CurFixtureCount = instance.Fixtures.Count;

        connection.SendTunneled(new HousingPacketFixtureRemove
        {
            FixtureGuid = fixtureGuid
        });

        SendHouseInfo(connection, true);

        return true;
    }

    public static bool SetEditMode(GatewayConnection connection, bool inEditMode)
    {
        SendHouseInfo(connection, inEditMode);
        return true;
    }

    private static void SendInstanceData(GatewayConnection connection, IResourceManager resourceManager)
    {
        var instance = GetOrCreateInstance(connection);
        instance.BuildAreas = CreateOverworldBuildAreas();
        instance.MaxFixtureCount = MaxFixtureCount;
        instance.CurFixtureCount = instance.Fixtures.Count;

        connection.SendTunneled(new HousingPacketInstanceData
        {
            InstanceData = instance
        });

        SendFixtureItemList(connection, resourceManager);
    }

    private static void SendHouseInfo(GatewayConnection connection, bool inEditMode)
    {
        var instance = GetOrCreateInstance(connection);

        connection.SendTunneled(new HousingPacketUpdateHouseInfo
        {
            InEditMode = inEditMode,
            IsLocked = false,
            PetAutospawn = false,
            CurFixtureCount = instance.Fixtures.Count,
            FurnitureScore = instance.FurnitureScore
        });
    }

    private static PlayerHousingInstanceData GetOrCreateInstance(GatewayConnection connection)
    {
        return Instances.GetOrAdd(connection.Player.Guid, _ => new PlayerHousingInstanceData
        {
            HouseGuid = MakeHouseGuid(connection.Player.Guid),
            OwnerGuid = connection.Player.Guid,
            OwnerName = connection.Player.Name.FullName,
            NameId = 0,
            Name = "Overworld Free Build",
            IsLocked = false,
            PetAutospawn = false,
            MaxFixtureCount = MaxFixtureCount,
            MaxLandmarkCount = MaxFixtureCount,
            IconId = 5599,
            IsMembersOnly = false,
            Unknown22 = string.Empty,
            Unknown23 = string.Empty,
            BuildAreas = CreateOverworldBuildAreas()
        });
    }

    private static PlayerHousingInstanceInfo CreateInstanceInfo(GatewayConnection connection)
    {
        var instance = GetOrCreateInstance(connection);

        return new PlayerHousingInstanceInfo
        {
            OwnerGuid = connection.Player.Guid,
            InstanceGuid = instance.HouseGuid,
            NameId = instance.NameId,
            OwnerName = connection.Player.Name.FullName,
            HouseName = instance.Name,
            IconId = instance.IconId,
            FixtureCount = instance.Fixtures.Count,
            FurnitureScore = instance.FurnitureScore,
            LastVisited = DateTime.UtcNow,
            IsLocked = false,
            IsMembersOnly = false,
            IsFloraAllowed = true,
            Description = "Overworld free build",
            KeywordList = "blocks,freebuild,overworld",
            Unknown21 = string.Empty,
            Rating = 0.0f,
            Votes = 0,
            HasRating = false,
            CanVote = false,
            FactoryPlotId = 0,
            WhenCreated = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    private static List<FixtureDefinition> GetFixtureDefinitions(IResourceManager resourceManager)
    {
        return resourceManager.ClientItemDefinitions.Values
            .Where(IsBlockFixtureDefinition)
            .GroupBy(x => x.ModelName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderBy(y => y.Id).First())
            .OrderBy(x => x.Id)
            .Select((x, index) => new FixtureDefinition
            {
                Id = x.Id,
                ItemDefinitionId = x.Id,
                Unknown3 = x.Param1,
                Unknown4 = index,
                Unknown11 = x.ModelName,
                Unknown12 = x.TextureAlias,
                Unknown5 = true,
                Unknown6 = true,
                Unknown7 = true,
                Unknown8 = true,
                Unknown9 = true,
                Unknown10 = true,
                CompositeEffectId = x.CompositeEffectId,
                Unknown14 = 1.0f,
                Unknown15 = 1.0f,
                Unknown16 = true,
                Unknown17 = true,
                Unknown18 = true,
                Unknown19 = true
            })
            .ToList();
    }

    private static bool IsBlockFixtureDefinition(ClientItemDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.ModelName) &&
               SafeBuildBlockModels.Contains(definition.ModelName);
    }

    private static int ResolveItemDefinitionId(IResourceManager resourceManager, int requestedItemDefinitionId, ulong requestedFixtureGuid)
    {
        if (requestedItemDefinitionId > 0 &&
            resourceManager.ClientItemDefinitions.TryGetValue(requestedItemDefinitionId, out var definition) &&
            IsBlockFixtureDefinition(definition))
            return requestedItemDefinitionId;

        if (FixtureDefinitionByGuid.TryGetValue(requestedFixtureGuid, out var mappedDefinitionId))
            return mappedDefinitionId;

        var lowGuidValue = (int)(requestedFixtureGuid & 0xffffffff);
        if (resourceManager.ClientItemDefinitions.TryGetValue(lowGuidValue, out var lowDefinition) &&
            IsBlockFixtureDefinition(lowDefinition))
            return lowGuidValue;

        return GetFixtureDefinitions(resourceManager).FirstOrDefault()?.ItemDefinitionId ?? 0;
    }

    private static FixtureInstance CreateFixture(ulong fixtureGuid, ulong houseGuid, int itemDefinitionId, Vector4 position, Quaternion rotation)
    {
        return new FixtureInstance
        {
            Guid = fixtureGuid,
            HouseGuid = houseGuid,
            Id = itemDefinitionId,
            Unknown4 = 0,
            Unknown5 = position,
            Unknown6 = rotation,
            Unknown7 = Quaternion.Identity,
            Unknown8 = 0,
            Unknown9 = 0,
            Unknown10 = 0,
            Unknown11 = string.Empty,
            Unknown12 = string.Empty,
            Unknown13 = 0,
            Unknown14 = string.Empty,
            Unknown15 = 1.0f,
            Unknown16 = true,
            Unknown17 = 0
        };
    }

    private static List<BoundingBox> CreateOverworldBuildAreas()
    {
        return
        [
            new()
            {
                Min = new Vector4(-100000.0f, -100000.0f, -100000.0f, 1.0f),
                Max = new Vector4(100000.0f, 100000.0f, 100000.0f, 1.0f)
            }
        ];
    }

    private static bool IsUsablePosition(Vector4 value)
    {
        return float.IsFinite(value.X) &&
               float.IsFinite(value.Y) &&
               float.IsFinite(value.Z) &&
               (value.X != 0.0f || value.Y != 0.0f || value.Z != 0.0f);
    }

    private static bool IsUsableRotation(Quaternion value)
    {
        return float.IsFinite(value.X) &&
               float.IsFinite(value.Y) &&
               float.IsFinite(value.Z) &&
               float.IsFinite(value.W) &&
               (value.X != 0.0f || value.Y != 0.0f || value.Z != 0.0f || value.W != 0.0f);
    }

    private static ulong MakeHouseGuid(ulong playerGuid)
    {
        return SyntheticHouseGuidBase | (playerGuid & 0x0000ffffffffffff);
    }

    private static ulong MakeSyntheticFixtureGuid(int itemDefinitionId)
    {
        return SyntheticFixtureGuidBase | (uint)itemDefinitionId;
    }

    private static int MakeSyntheticBuildItemGuid(int itemDefinitionId)
    {
        return SyntheticBuildItemGuidBase + itemDefinitionId;
    }

    private static ulong MakePlacedFixtureGuid(GatewayConnection connection, int index)
    {
        return SyntheticFixtureGuidBase | ((connection.Player.Guid & 0xffff) << 32) | (uint)index;
    }
}
