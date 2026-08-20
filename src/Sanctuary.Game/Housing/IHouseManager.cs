using System.Collections.Generic;

using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Housing;

public enum EnterHouseResult
{
    Success,
    HouseNotFound,
    NotAuthorized,
    UnsupportedSourceZone,
    ZoneUnavailable,
    TransferFailed
}

public interface IHouseManager
{
    IReadOnlyList<DbHouse> GetOwnedHouses(ulong characterId);

    EnterHouseResult EnterOwnedHouse(Player player, int zoneDefinitionId);
    EnterHouseResult EnterHouse(Player player, ulong houseGuid);
    EnterHouseResult VisitHouse(Player player, int zoneDefinitionId, string ownerName);

    bool LeaveHouse(Player player);
}
