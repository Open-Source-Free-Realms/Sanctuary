using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.Zones;

public sealed class HousingZone : BaseZone
{
    private const int HousingZoneHeadSize = 10;

    private bool _isHouseLoaded;

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

        if (PetAutospawn && IsOwner(player) && player.Pet is null)
            player.PlayerPet.QueueAction(() => player.PlayerPet.SummonPet(player.ActivePetId));
    }

    public bool IsOwner(Player player) => OwnerId == player.Guid && !player.IsPreviewingHouse;

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

    public void SetPetAutospawn(bool petAutospawn) => UpdateHouse(dbHouse => dbHouse.PetAutospawn = petAutospawn);

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

    private void LoadHouse()
    {
        if (_isHouseLoaded || OwnerId is not { } ownerGuid)
            return;

        _isHouseLoaded = true;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.AsNoTracking().FirstOrDefault(house => house.Id == DefinitionId && house.CharacterId == GuidHelper.GetPlayerId(ownerGuid));

        if (dbHouse is not null)
            ApplyHouse(dbHouse);
    }

    private void UpdateHouse(Action<DbHouse> update)
    {
        if (OwnerId is not { } ownerGuid)
            return;

        var characterId = GuidHelper.GetPlayerId(ownerGuid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.FirstOrDefault(house => house.Id == DefinitionId && house.CharacterId == characterId);

        if (dbHouse is null)
        {
            dbHouse = new DbHouse
            {
                Id = DefinitionId,
                Name = HouseName,
                IsLocked = IsLocked,
                IsFloraAllowed = IsFloraAllowed,
                PetAutospawn = PetAutospawn,
                CharacterId = characterId
            };

            dbContext.Houses.Add(dbHouse);
        }

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
        if (!HousingHelper.TryGetHouseDefinition(_resourceManager, DefinitionId, out var houseDefinition) || OwnerId is not { } ownerGuid)
            return;

        var ownerName = GetOwnerName(ownerGuid);

        var instanceInfo = HousingHelper.GetInstanceInfo(ownerGuid, ownerName, houseDefinition);

        instanceInfo.HouseName = HouseName;
        instanceInfo.IsLocked = IsLocked;
        instanceInfo.IsFloraAllowed = IsFloraAllowed;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.AsNoTracking().Include(house => house.Votes)
            .FirstOrDefault(house => house.Id == DefinitionId && house.CharacterId == GuidHelper.GetPlayerId(ownerGuid));

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
        if (!HousingHelper.TryGetHouseDefinition(_resourceManager, DefinitionId, out var houseDefinition) || OwnerId is not { } ownerGuid)
            return;

        var ownerName = GetOwnerName(ownerGuid);

        var instanceData = HousingHelper.GetInstanceData(ownerGuid, ownerName, houseDefinition);

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