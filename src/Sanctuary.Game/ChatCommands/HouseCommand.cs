using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.ChatCommands;

public class HouseCommand : IChatCommand
{
    private readonly IResourceManager _resourceManager;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "house";
    public string Usage => "visit <zone>|leave";
    public string Description => "Visits a housing zone or leaves the current one.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public HouseCommand(IResourceManager resourceManager, IZoneManager zoneManager)
    {
        _resourceManager = resourceManager;
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args is ["leave"])
        {
            if (!HousingHelper.TryLeaveHouse(invoker, _zoneManager))
                ChatHelper.SendSystemMessage(invoker, "You are not in a housing zone.");

            return true;
        }

        if (args is not ["visit", var zoneName])
            return false;

        var zoneDefinition = _resourceManager.Zones.Values.OfType<HousingZoneDefinition>()
            .FirstOrDefault(zone => zone.Name == zoneName);

        if (zoneDefinition is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"No housing zone named {zoneName}.");
            return true;
        }

        if (zoneDefinition.IsBroken)
        {
            ChatHelper.SendSystemMessage(invoker, $"{zoneName} is marked broken and cannot be visited.");
            return true;
        }

        var houseKey = HousingHelper.TryGetHouseDefinition(_resourceManager, zoneDefinition.Id, out var houseDefinition)
            ? HousingHelper.GetReservedHouseKey(houseDefinition)
            : HousingHelper.PreviewHouseKey;

        if (!invoker.TeleportToZone(zoneDefinition.Id, HousingHelper.GetHouseGuid(invoker.Guid, houseKey), zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation))
            ChatHelper.SendSystemMessage(invoker, $"Could not visit {zoneName}.");

        return true;
    }
}
