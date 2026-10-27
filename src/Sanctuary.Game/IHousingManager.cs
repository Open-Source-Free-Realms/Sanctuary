using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

public interface IHousingManager
{
    bool IsFurniture(ClientItemDefinition itemDefinition);

    bool TryGetFixtureDefinition(int itemDefinitionId, [MaybeNullWhen(false)] out FixtureDefinition fixtureDefinition);

    List<DbHouse> GetHouses(ulong characterId);

    void ProvisionHousesFromItems(Player player);

    bool TryGrantHouse(Player player, HouseDefinition houseDefinition, [MaybeNullWhen(false)] out DbHouse house);

    PlayerHousingInstanceInfo GetInstanceInfo(DbHouse house, string? ownerName);

    void SendPlayerHouses(Player requester, ulong playerGuid, string? playerName);

    bool TryEnterHouse(Player player, ulong houseGuid);

    bool TryLeaveHouse(Player player);

    bool TryVisitPlayer(Player player, ulong playerGuid);

    int GrantAllFurniture(Player player, int quantity);

    void SetHouseLocked(Player player, ulong houseGuid, bool isLocked);
}
