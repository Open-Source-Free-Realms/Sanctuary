using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Extensions;
using Sanctuary.Core.Helpers;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Zones;

public sealed class HousingZone : BaseZone
{
    private const int HousingZoneHeadSize = 10;

    private const float HousePetSpeed = 6f;
    private const int HousePetUpdateIntervalMs = 500;
    private const float HousePetMinLegDistance = 5f;
    private const float HousePetMaxLegDistance = 20f;
    private const int HousePetMinPauseMs = 1000;
    private const int HousePetMaxPauseMs = 3000;
    private const float HousePetSpawnRadius = 1.5f;
    private readonly Dictionary<int, Npc> _housePets = [];

    private bool _isHouseLoaded;
    private ulong? _ownerGuid;
    private int _houseKey;

    public string HouseName { get; private set; } = string.Empty;

    public bool IsLocked { get; private set; }
    public bool IsFloraAllowed { get; private set; } = true;
    public bool PetAutospawn { get; private set; }

    public bool InEditMode { get; private set; }

    public HousingZone(HousingZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
    }

    public override void OnClientIsReady(Player player)
    {
        LoadHouse();

        base.OnClientIsReady(player);

        SendShopData(player);
        SendHousingZoneData(player);

        if (PetAutospawn && IsOwner(player))
            SpawnHousePets(player);
    }

    public ulong? OwnerGuid
    {
        get
        {
            LoadHouse();
            return _ownerGuid;
        }
    }

    public bool IsOwner(Player player) => OwnerGuid == player.Guid && !player.IsPreviewingHouse;

    public void SetEditMode(Player player, bool inEditMode)
    {
        InEditMode = inEditMode;

        SendHouseInfo(player);
    }

    public void SetHouseName(string houseName) => UpdateHouse(dbHouse => dbHouse.Name = houseName);

    public void SetLocked(bool isLocked) => UpdateHouse(dbHouse => dbHouse.IsLocked = isLocked);

    public void SetFloraAllowed(bool isFloraAllowed)
    {
        UpdateHouse(dbHouse => dbHouse.IsFloraAllowed = isFloraAllowed);

        foreach (var player in Players)
            SendZoneData(player);
    }

    public void SetPetAutospawn(Player player, bool petAutospawn)
    {
        UpdateHouse(dbHouse => dbHouse.PetAutospawn = petAutospawn);

        if (PetAutospawn)
            SpawnHousePets(player);
        else
            RemoveHousePets();
    }

    public void SendHouseInfo(Player player)
    {
        player.SendTunneled(new HousingPacketUpdateHouseInfo
        {
            InEditMode = InEditMode,
            IsLocked = IsLocked,
            IsFloraAllowed = IsFloraAllowed,
            PetAutospawn = PetAutospawn
        });
    }

    private void SpawnHousePets(Player owner)
    {
        if (!HousingHelper.TryGetHouseDefinition(_resourceManager, DefinitionId, out var houseDefinition)
            || houseDefinition.NoHousePets || houseDefinition.BuildAreas.Count == 0 || !_resourceManager.Zones.TryGetValue(DefinitionId, out var zoneDefinition))
            return;

        var spawnPosition = zoneDefinition.SpawnPosition;

        foreach (var petClientInfo in owner.Pets)
        {
            if (petClientInfo.Id == owner.ActivePetId || _housePets.ContainsKey(petClientInfo.Id)
                || !PetHelper.TryGetPetModel(_resourceManager, owner, petClientInfo.Id, out _, out var clientItemDefinition, out var modelDefinition)
                || !TryCreateNpc(out var npc))
            {
                continue;
            }

            PetHelper.ApplyPetAppearance(_resourceManager, owner, npc, petClientInfo, clientItemDefinition, modelDefinition);

            var heading = Random.Shared.NextSingle() * MathF.Tau;
            var position = new Vector4(spawnPosition.X + MathF.Sin(heading) * HousePetSpawnRadius, spawnPosition.Y,
                spawnPosition.Z + MathF.Cos(heading) * HousePetSpawnRadius, 1f);

            npc.Visible = true;
            npc.UpdatePosition(position, QuaternionExtensions.FromHeading(heading));
            npc.MovementController = CreateHousePetMovement(npc, houseDefinition.BuildAreas[0]);

            _housePets[petClientInfo.Id] = npc;
        }
    }

    public bool TryTakeHousePet(int petId, [MaybeNullWhen(false)] out Npc npc)
    {
        if (!_housePets.Remove(petId, out npc))
            return false;

        npc.MovementController = null;
        return true;
    }

    public bool TryReturnHousePet(Player player, int petId, Npc npc)
    {
        if (!PetAutospawn || !IsOwner(player) || _housePets.ContainsKey(petId)
            || !HousingHelper.TryGetHouseDefinition(_resourceManager, DefinitionId, out var houseDefinition)
            || houseDefinition.NoHousePets || houseDefinition.BuildAreas.Count == 0)
        {
            return false;
        }

        npc.PositionSendIntervalMs = default;
        npc.MovementController = CreateHousePetMovement(npc, houseDefinition.BuildAreas[0]);
        _housePets[petId] = npc;
        return true;
    }

    private static NpcMovementController CreateHousePetMovement(Npc npc, BoundingBox buildArea) => new(npc, new NpcMovementDefinition
    {
        Type = NpcMovementType.Wander,
        Speed = HousePetSpeed,
        UpdateIntervalMs = HousePetUpdateIntervalMs,
        Radius = Vector3.Distance(buildArea.Min.ToVector3(), buildArea.Max.ToVector3()),
        MinLegDistance = HousePetMinLegDistance,
        MaxLegDistance = HousePetMaxLegDistance,
        MinPauseMs = HousePetMinPauseMs,
        MaxPauseMs = HousePetMaxPauseMs,
        Area =
        [
            [buildArea.Min.X, buildArea.Min.Y, buildArea.Min.Z],
            [buildArea.Max.X, buildArea.Min.Y, buildArea.Min.Z],
            [buildArea.Max.X, buildArea.Min.Y, buildArea.Max.Z],
            [buildArea.Min.X, buildArea.Min.Y, buildArea.Max.Z]
        ]
    });

    private void RemoveHousePets()
    {
        foreach (var npc in _housePets.Values)
            EntityHelper.RemovePlayerGracefully(npc);

        _housePets.Clear();
    }

    private void LoadHouse()
    {
        if (_isHouseLoaded || OwnerId is not { } houseGuid)
            return;

        _isHouseLoaded = true;

        if (!HousingHelper.TryGetHouse(_dbContextFactory, houseGuid, out var dbHouse))
            return;

        _ownerGuid = HousingHelper.GetHouseOwnerGuid(dbHouse);
        _houseKey = dbHouse.Key;

        ApplyHouse(dbHouse);
    }

    private void UpdateHouse(Action<DbHouse> update)
    {
        LoadHouse();

        if (OwnerId is not { } houseGuid || _houseKey == HousingHelper.PreviewHouseKey)
            return;

        var houseId = GuidHelper.GetHouseId(houseGuid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.FirstOrDefault(house => house.Id == houseId);

        if (dbHouse is null)
            return;

        update(dbHouse);
        dbContext.SaveChanges();

        ApplyHouse(dbHouse);

        foreach (var player in Players.Where(IsOwner))
        {
            SendHouseInfo(player);
            player.SendTunneled(HousingHelper.GetInstanceList(_resourceManager, _dbContextFactory, player));
        }
    }

    private void ApplyHouse(DbHouse dbHouse)
    {
        HouseName = dbHouse.Name;
        IsLocked = dbHouse.IsLocked;
        IsFloraAllowed = dbHouse.IsFloraAllowed;
        PetAutospawn = dbHouse.PetAutospawn;
    }

    private void SendHousingZoneData(Player player)
    {
        SendZoneData(player);
        SendInstanceData(player);
    }

    private void SendZoneData(Player player)
    {
        if (!HousingHelper.TryGetHouseDefinition(_resourceManager, DefinitionId, out var houseDefinition) || OwnerId is not { } houseGuid || OwnerGuid is not { } ownerGuid)
            return;

        var instanceInfo = HousingHelper.GetInstanceInfo(houseGuid, ownerGuid, GetOwnerName(ownerGuid), houseDefinition);

        instanceInfo.HouseName = HouseName;
        instanceInfo.IsLocked = IsLocked;
        instanceInfo.IsFloraAllowed = IsFloraAllowed;

        var houseId = GuidHelper.GetHouseId(houseGuid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.AsNoTracking().Include(house => house.Votes).FirstOrDefault(house => house.Id == houseId);

        if (dbHouse is not null)
            HousingHelper.SetRatingInfo(instanceInfo, dbHouse, player.Guid);

        player.SendTunneled(new HousingPacketZoneData
        {
            IsPreview = player.IsPreviewingHouse,
            HeadSize = HousingZoneHeadSize,
            InstanceInfo = instanceInfo
        });
    }

    private void SendInstanceData(Player player)
    {
        if (!HousingHelper.TryGetHouseDefinition(_resourceManager, DefinitionId, out var houseDefinition) || OwnerId is not { } houseGuid || OwnerGuid is not { } ownerGuid)
            return;

        var instanceData = HousingHelper.GetInstanceData(houseGuid, ownerGuid, GetOwnerName(ownerGuid), houseDefinition);

        instanceData.Name = HouseName;
        instanceData.IsLocked = IsLocked;
        instanceData.IsFloraAllowed = IsFloraAllowed;
        instanceData.Preview = player.IsPreviewingHouse;

        player.SendTunneled(new HousingPacketInstanceData
        {
            InstanceData = instanceData
        });
    }

    private string GetOwnerName(ulong ownerGuid) => Players.FirstOrDefault(zonePlayer => zonePlayer.Guid == ownerGuid)?.Name.FullName ?? string.Empty;
}