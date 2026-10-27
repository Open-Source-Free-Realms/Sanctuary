using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Zones;

/// <summary>
/// GM world placement: props placed into a world zone, spawned for everyone and persisted.
/// </summary>
public sealed partial class WorldZone
{
    /// <summary>
    /// House guid used for the stand-in housing instance that lets GMs use the
    /// client's fixture placement tools in a world zone. Never stored in the database.
    /// </summary>
    private static readonly ulong WorldPlacementHouseGuid = GuidHelper.GetHouseGuid(0);

    /// <summary>
    /// <see cref="InstancePermission.Level"/> that allows editing.
    /// </summary>
    private const int EditPermissionLevel = 3;

    private const int DefaultHeadSize = 10;

    /// <summary>
    /// Placed props within this range are handed to the client's furniture tool so they can be moved.
    /// </summary>
    private const float EditableFixtureRange = 250.0f;

    private readonly IHousingManager _housingManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly object _worldFixtureLock = new();
    private readonly Dictionary<ulong, DbWorldFixture> _worldFixtures = new();
    private readonly ConcurrentDictionary<ulong, WorldPlacementSession> _placementSessions = new();

    private bool _worldFixturesLoaded;

    /// <summary>
    /// <paramref name="ItemDefinitionId"/> 0 is a move-only session: existing props can be moved but nothing is stamped.
    /// </summary>
    private sealed record WorldPlacementSession(int ItemDefinitionId, int TintId, float Scale);

    #region World Fixtures

    public List<(Npc Npc, DbWorldFixture Fixture)> GetWorldFixtures()
    {
        lock (_worldFixtureLock)
        {
            var worldFixtures = new List<(Npc, DbWorldFixture)>();

            foreach (var (npcGuid, fixture) in _worldFixtures)
            {
                if (TryGetNpc(npcGuid, out var npc))
                    worldFixtures.Add((npc, fixture));
            }

            return worldFixtures;
        }
    }

    private void LoadWorldFixtures()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var fixtures = dbContext.WorldFixtures
            .AsNoTracking()
            .Where(x => x.Zone == DefinitionId)
            .ToList();

        foreach (var fixture in fixtures)
        {
            if (!TrySpawnWorldFixture(fixture, out _))
                Logger.LogWarning("Failed to spawn world fixture {id} (item {definition}).", fixture.Id, fixture.Definition);
        }

        if (fixtures.Count > 0)
            Logger.LogInformation("Spawned {count} world fixture(s).", fixtures.Count);
    }

    private bool TrySpawnWorldFixture(DbWorldFixture fixture, [MaybeNullWhen(false)] out Npc npc)
    {
        npc = null;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(fixture.Definition, out var itemDefinition)
            || !_housingManager.TryGetFixtureDefinition(fixture.Definition, out var fixtureDefinition))
            return false;

        if (!TryCreateNpc(out npc))
            return false;

        npc.NameId = itemDefinition.NameId;
        npc.ModelId = fixtureDefinition.ModelId;
        npc.TextureAlias = itemDefinition.TextureAlias;
        npc.TintAlias = itemDefinition.TintAlias;
        npc.TintId = fixture.Tint;
        npc.Scale = fixture.Scale;
        npc.HideNamePlate = true;
        npc.IsInteractable = false;
        npc.Visible = true;

        // No idle loop unless a GM sets one (see HousingZone.SpawnFixtureNpc).
        npc.Animation = 0;

        var position = new Vector4(fixture.PositionX, fixture.PositionY, fixture.PositionZ, 1.0f);
        var rotation = QuaternionExtensions.FromHeading(GetWorldFixtureHeading(fixture));

        npc.SpawnPosition = position;
        npc.SpawnRotation = rotation;

        NpcProperties.Apply(npc, fixture.Properties);

        // A placed object, so the client neither simulates nor animates it (see HousingZone.GetFixtureTilt).
        npc.Tilt ??= new Vector4(GetWorldFixtureHeading(fixture), 0f, 0f, 0f);

        npc.UpdatePosition(position, rotation);

        npc.IsWorldProp = true;

        if (!fixture.Collidable)
            npc.SetCollidable(false);

        _worldFixtures.Add(npc.Guid, fixture);

        AttachSavedScript(npc, fixture.Script);

        return true;
    }

    /// <summary>
    /// Turns collision on a placed prop on or off for everyone and saves it.
    /// </summary>
    public bool TrySetWorldFixtureCollidable(ulong npcGuid, bool collidable)
    {
        lock (_worldFixtureLock)
        {
            if (!_worldFixtures.TryGetValue(npcGuid, out var fixture) || !TryGetNpc(npcGuid, out var npc))
                return false;

            fixture.Collidable = collidable;

            using (var dbContext = _dbContextFactory.CreateDbContext())
            {
                dbContext.WorldFixtures.Update(fixture);
                dbContext.SaveChanges();
            }

            npc.SetCollidable(collidable);

            return true;
        }
    }

    /// <summary>
    /// Saves a placed prop's name, animation and appearance (see <see cref="NpcProperties"/>).
    /// </summary>
    public bool TrySaveWorldFixtureProperties(Npc npc)
    {
        lock (_worldFixtureLock)
        {
            if (!_worldFixtures.TryGetValue(npc.Guid, out var fixture))
                return false;

            fixture.Properties = NpcProperties.Capture(npc, includeBasics: true);

            // Stored like the client sends it: yaw in X (pitch and roll live in Properties).
            fixture.RotationX = NpcProperties.GetHeading(npc.SpawnRotation);
            fixture.RotationY = 0f;
            fixture.RotationZ = 0f;
            fixture.RotationW = 0f;

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldFixtures.Update(fixture);
            dbContext.SaveChanges();

            return true;
        }
    }

    /// <summary>
    /// A prop's heading in radians. The client places with its angles (yaw in X); rows saved as an NPC facing
    /// direction (X/Z with W = 0) or a quaternion by earlier builds are read as such.
    /// </summary>
    private static float GetWorldFixtureHeading(DbWorldFixture fixture)
    {
        if (fixture.RotationW != 0f || fixture.RotationZ != 0f)
            return NpcProperties.GetHeading(new Quaternion(fixture.RotationX, fixture.RotationY, fixture.RotationZ, fixture.RotationW));

        return fixture.RotationX;
    }

    private bool TrySaveWorldFixtureScript(ulong npcGuid, string? source)
    {
        lock (_worldFixtureLock)
        {
            if (!_worldFixtures.TryGetValue(npcGuid, out var fixture))
                return false;

            fixture.Script = source;

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldFixtures.Update(fixture);
            dbContext.SaveChanges();

            return true;
        }
    }

    public bool TryPlaceWorldFixture(Player player, int itemDefinitionId, int tintId, Vector4 position, Quaternion rotation, float scale,
        [MaybeNullWhen(false)] out Npc npc)
    {
        npc = null;

        if (!_housingManager.TryGetFixtureDefinition(itemDefinitionId, out _))
            return false;

        lock (_worldFixtureLock)
        {
            var fixture = new DbWorldFixture
            {
                Zone = DefinitionId,
                Definition = itemDefinitionId,
                Tint = tintId,
                PositionX = position.X,
                PositionY = position.Y,
                PositionZ = position.Z,
                RotationX = rotation.X,
                RotationY = rotation.Y,
                RotationZ = rotation.Z,
                RotationW = rotation.W,
                Scale = scale > 0.0f ? scale : 1.0f,
                CreatedBy = GuidHelper.GetPlayerId(player.Guid)
            };

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldFixtures.Add(fixture);

            if (dbContext.SaveChanges() <= 0)
                return false;

            if (TrySpawnWorldFixture(fixture, out npc))
                return true;

            dbContext.WorldFixtures.Remove(fixture);
            dbContext.SaveChanges();

            return false;
        }
    }

    public bool TryRemoveWorldFixture(ulong npcGuid, [MaybeNullWhen(false)] out DbWorldFixture fixture)
    {
        lock (_worldFixtureLock)
        {
            if (!_worldFixtures.Remove(npcGuid, out fixture))
                return false;

            using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.WorldFixtures.Remove(fixture);
            dbContext.SaveChanges();

            if (TryGetNpc(npcGuid, out var npc))
                npc.Dispose();

            return true;
        }
    }

    public bool IsWorldFixture(ulong npcGuid)
    {
        lock (_worldFixtureLock)
            return _worldFixtures.ContainsKey(npcGuid);
    }

    public bool TryGetWorldFixture(ulong npcGuid, [MaybeNullWhen(false)] out DbWorldFixture fixture)
    {
        lock (_worldFixtureLock)
            return _worldFixtures.TryGetValue(npcGuid, out fixture);
    }

    /// <summary>
    /// Finds a world fixture by the fixture guid the client's furniture tool uses.
    /// </summary>
    private bool TryFindWorldFixture(ulong fixtureGuid, out ulong npcGuid, [MaybeNullWhen(false)] out DbWorldFixture fixture)
    {
        foreach (var (guid, worldFixture) in _worldFixtures)
        {
            if (GuidHelper.GetFixtureGuid(worldFixture.Id) != fixtureGuid)
                continue;

            npcGuid = guid;
            fixture = worldFixture;
            return true;
        }

        npcGuid = 0;
        fixture = null;
        return false;
    }

    public bool TryGetNearestWorldFixture(Vector4 position, float radius, [MaybeNullWhen(false)] out Npc npc)
    {
        npc = GetWorldFixtures()
            .Select(x => x.Npc)
            .Where(x => Vector4.DistanceSquared(position, x.Position) <= radius * radius)
            .MinBy(x => Vector4.DistanceSquared(position, x.Position));

        return npc is not null;
    }

    #endregion

    #region Placement Mode

    public bool IsPlacing(Player player) => _placementSessions.ContainsKey(player.Guid);

    /// <summary>
    /// Puts the player into the client's housing placement mode for the given item.
    /// Every fixture they place is spawned into this zone for everyone and persisted.
    /// </summary>
    public bool TryBeginPlacement(Player player, int itemDefinitionId, int tintId, float scale)
    {
        FixtureDefinition? fixtureDefinition = null;

        if (itemDefinitionId != 0 && !_housingManager.TryGetFixtureDefinition(itemDefinitionId, out fixtureDefinition))
            return false;

        _placementSessions[player.Guid] = new WorldPlacementSession(itemDefinitionId, tintId, scale > 0.0f ? scale : 1.0f);

        // The client only allows placement inside a housing instance it can edit, so
        // hand it an empty instance owned by the GM that covers the whole zone.
        var instanceData = new PlayerHousingInstanceData
        {
            HouseGuid = WorldPlacementHouseGuid,
            OwnerGuid = player.Guid,
            OwnerName = player.Name.FullName,
            Name = Name,
            IsFloraAllowed = true,
            MaxFixtureCount = int.MaxValue,
            BuildAreas =
            [
                new BoundingBox
                {
                    // W is compared too, so it has to enclose the fixture's.
                    Min = new Vector4(-100_000.0f, -100_000.0f, -100_000.0f, -1.0f),
                    Max = new Vector4(100_000.0f, 100_000.0f, 100_000.0f, 1.0f)
                }
            ]
        };

        instanceData.Permissions.Add(0, new InstancePermission
        {
            Guid = player.Guid,
            Level = EditPermissionLevel
        });

        // Turns on the client's housing tools, the instance data then makes them editable.
        player.SendTunneled(new HousingPacketZoneData
        {
            IsInInstance = true,
            HeadSize = DefaultHeadSize,
            InstanceInfo =
            {
                OwnerGuid = player.Guid,
                InstanceGuid = WorldPlacementHouseGuid,
                OwnerName = player.Name.FullName,
                HouseName = Name
            }
        });

        player.SendTunneled(new HousingPacketInstanceData
        {
            InstanceData = instanceData
        });

        // Must follow the instance, the client's fixture list handler assumes one exists.
        var housingPacketFixtureItemList = new HousingPacketFixtureItemList();

        if (fixtureDefinition is not null)
            housingPacketFixtureItemList.Definitions.Add(fixtureDefinition);

        player.SendTunneled(housingPacketFixtureItemList);

        player.SendTunneled(new HousingPacketUpdateHouseInfo
        {
            InEditMode = true,
            IsFloraAllowed = true
        });

        SendEditableWorldFixtures(player);

        SendPlacementAsset(player);

        return true;
    }

    /// <summary>
    /// Opens the furniture tool without an item to stamp, so the GM can move and pick up placed props.
    /// </summary>
    public bool TryBeginMoving(Player player) => TryBeginPlacement(player, 0, 0, 1.0f);

    public bool EndPlacement(Player player)
    {
        if (!_placementSessions.TryRemove(player.Guid, out _))
            return false;

        player.SendTunneled(new HousingPacketUpdateHouseInfo());

        player.SendTunneled(new HousingPacketZoneData
        {
            HeadSize = DefaultHeadSize
        });

        return true;
    }

    public void RequestPlacement(Player player)
    {
        SendPlacementAsset(player);
    }

    public void PlaceFixture(Player player, ClientHousingPacketPlaceFixture packet)
    {
        if (!_placementSessions.TryGetValue(player.Guid, out var session))
            return;

        var scale = packet.Scale > 0.0f ? packet.Scale * session.Scale : session.Scale;

        if (!TryPlaceWorldFixture(player, session.ItemDefinitionId, session.TintId, packet.Position, packet.Rotation, scale, out var npc))
        {
            player.SendTunneled(new HousingPacketFixturePlacementDenied
            {
                Message = "Fixture placement denied."
            });

            return;
        }

        Logger.LogInformation("{player} placed world fixture item {item} as {npc} at {position}.",
            player.Name.FullName, session.ItemDefinitionId, npc.Guid, npc.Position);

        // Keep stamping the same item until the GM leaves placement mode.
        SendPlacementAsset(player);
    }

    /// <summary>
    /// Hands nearby placed props to the client's furniture tool, as a house does with its fixtures.
    /// </summary>
    private void SendEditableWorldFixtures(Player player)
    {
        foreach (var (npc, fixture) in GetWorldFixtures())
        {
            if (Vector4.DistanceSquared(player.Position, npc.Position) <= EditableFixtureRange * EditableFixtureRange)
                player.SendTunneled(GetWorldFixtureUpdate(npc, fixture));
        }
    }

    private HousingPacketFixtureUpdate GetWorldFixtureUpdate(Npc npc, DbWorldFixture fixture)
    {
        var fixtureGuid = GuidHelper.GetFixtureGuid(fixture.Id);

        var housingPacketFixtureUpdate = new HousingPacketFixtureUpdate
        {
            Instance = new FixtureInstance
            {
                Guid = fixtureGuid,
                HouseGuid = WorldPlacementHouseGuid,
                FixtureDefinitionId = fixture.Definition,
                Position = new Vector4(fixture.PositionX, fixture.PositionY, fixture.PositionZ, 1.0f),
                Rotation = new Quaternion(fixture.RotationX, fixture.RotationY, fixture.RotationZ, fixture.RotationW),
                Scale = fixture.Scale,
                TintId = fixture.Tint,
                // Links the tool to the prop's existing actor.
                NpcGuid = npc.Guid
            },
            Info = new FixtureInstanceInfo
            {
                FixtureGuid = fixtureGuid,
                ItemDefinitionId = fixture.Definition
            }
        };

        if (_housingManager.TryGetFixtureDefinition(fixture.Definition, out var fixtureDefinition))
            housingPacketFixtureUpdate.Definition = fixtureDefinition;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(fixture.Definition, out var itemDefinition))
            return housingPacketFixtureUpdate;

        // The client ignores Move/Turn/Remove on a fixture whose model name (or definition category) is empty.
        housingPacketFixtureUpdate.Instance.Unknown12 = itemDefinition.ModelName;

        if (!string.IsNullOrEmpty(itemDefinition.TextureAlias))
        {
            housingPacketFixtureUpdate.Instance.Customization.Type = 1;
            housingPacketFixtureUpdate.Instance.Customization.TextureAlias = itemDefinition.TextureAlias;
            housingPacketFixtureUpdate.Instance.Customization.TintAlias = itemDefinition.TintAlias;
            housingPacketFixtureUpdate.Instance.Customization.TintId = fixture.Tint;
        }

        return housingPacketFixtureUpdate;
    }

    /// <summary>
    /// The GM moved (or retinted/rescaled) a prop with the furniture tool.
    /// </summary>
    public void SaveFixture(Player player, ClientHousingPacketSaveFixture packet)
    {
        if (!IsPlacing(player))
            return;

        lock (_worldFixtureLock)
        {
            if (!TryFindWorldFixture(packet.FixtureGuid, out var npcGuid, out var fixture))
                return;

            var tint = packet.TintId > 0 ? packet.TintId : fixture.Tint;
            var scale = packet.Scale > 0.0f ? packet.Scale : fixture.Scale;
            var appearanceChanged = tint != fixture.Tint || scale != fixture.Scale;

            fixture.PositionX = packet.Position.X;
            fixture.PositionY = packet.Position.Y;
            fixture.PositionZ = packet.Position.Z;
            fixture.RotationX = packet.Rotation.X;
            fixture.RotationY = packet.Rotation.Y;
            fixture.RotationZ = packet.Rotation.Z;
            fixture.RotationW = packet.Rotation.W;
            fixture.Tint = tint;
            fixture.Scale = scale;

            using (var dbContext = _dbContextFactory.CreateDbContext())
            {
                dbContext.WorldFixtures.Update(fixture);
                dbContext.SaveChanges();
            }

            if (!TryGetNpc(npcGuid, out var npc))
                return;

            var position = packet.Position with { W = 1.0f };

            // The packet carries the client's angles (yaw in X); the NPC takes a facing direction.
            var facing = QuaternionExtensions.FromHeading(GetWorldFixtureHeading(fixture));

            npc.SpawnPosition = position;
            npc.SpawnRotation = facing;
            npc.Tilt = npc.Tilt is { } tilt ? tilt with { X = GetWorldFixtureHeading(fixture) } : null;

            if (appearanceChanged)
            {
                npc.TintId = tint;
                npc.Scale = scale;
                npc.UpdatePosition(position, facing);
                npc.RefreshForVisiblePlayers();

                player.SendTunneled(GetWorldFixtureUpdate(npc, fixture));
            }
            else
            {
                // Moves the prop for everyone in view.
                npc.UpdatePosition(position, facing);

                player.SendTunneled(new HousingPacketUpdateFixturePosition
                {
                    FixtureGuid = packet.FixtureGuid,
                    Position = position,
                    Rotation = packet.Rotation
                });
            }

            Logger.LogInformation("{player} moved world fixture {id} to {position}.", player.Name.FullName, fixture.Id, position);
        }
    }

    /// <summary>
    /// Changes a placed prop's scale and tint for everyone and saves them.
    /// </summary>
    public bool TrySetWorldFixtureAppearance(ulong npcGuid, float scale, int tint)
    {
        lock (_worldFixtureLock)
        {
            if (!_worldFixtures.TryGetValue(npcGuid, out var fixture) || !TryGetNpc(npcGuid, out var npc))
                return false;

            fixture.Scale = scale;
            fixture.Tint = tint;

            using (var dbContext = _dbContextFactory.CreateDbContext())
            {
                dbContext.WorldFixtures.Update(fixture);
                dbContext.SaveChanges();
            }

            npc.Scale = scale;
            npc.TintId = tint;
            npc.RefreshForVisiblePlayers();

            return true;
        }
    }

    /// <summary>
    /// The GM picked a prop up with the furniture tool, which deletes it.
    /// </summary>
    public void PickupFixture(Player player, ulong fixtureGuid)
    {
        if (!IsPlacing(player))
            return;

        ulong npcGuid;

        lock (_worldFixtureLock)
        {
            if (!TryFindWorldFixture(fixtureGuid, out npcGuid, out _))
                return;
        }

        if (!TryRemoveWorldFixture(npcGuid, out var fixture))
            return;

        player.SendTunneled(new HousingPacketFixtureRemove { FixtureGuid = fixtureGuid });

        Logger.LogInformation("{player} picked up world fixture {id} (item {definition}).", player.Name.FullName, fixture.Id, fixture.Definition);
    }

    private void SendPlacementAsset(Player player)
    {
        if (!_placementSessions.TryGetValue(player.Guid, out var session)
            || !_housingManager.TryGetFixtureDefinition(session.ItemDefinitionId, out var fixtureDefinition))
            return;

        _resourceManager.ClientItemDefinitions.TryGetValue(session.ItemDefinitionId, out var itemDefinition);

        player.SendTunneled(new HousingPacketFixtureAsset
        {
            ModelId = fixtureDefinition.ModelId,
            // Echoed back in the place packet; the session already knows which item it is.
            ItemGuid = session.ItemDefinitionId,
            TextureAlias = itemDefinition?.TextureAlias,
            TintAlias = itemDefinition?.TintAlias,
            TintId = session.TintId,
            Definition = fixtureDefinition
        });
    }

    #endregion
}
