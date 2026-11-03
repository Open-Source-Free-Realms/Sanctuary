using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.ChatCommands;

public class GoChatCommand : IChatCommand
{
    private readonly IResourceManager _resourceManager;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "go";
    public string Usage => "<zone name|zone id>|back";
    public string Description => "Enters a zone in your own instance, or goes back to where you left the world.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public GoChatCommand(IResourceManager resourceManager, IZoneManager zoneManager)
    {
        _resourceManager = resourceManager;
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args is ["back"])
        {
            invoker.TeleportToZone(_zoneManager.StartingZoneDefinitionId, null, invoker.StartingZonePosition, invoker.StartingZoneRotation);
            return true;
        }

        if (args is not [var zoneName])
            return false;

        var zoneDefinition = int.TryParse(zoneName, out var zoneDefinitionId) && _resourceManager.Zones.TryGetValue(zoneDefinitionId, out var definition)
            ? definition
            : _resourceManager.Zones.Values.FirstOrDefault(zone => string.Equals(zone.Name, zoneName, StringComparison.OrdinalIgnoreCase));

        if (zoneDefinition is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"No zone named {zoneName}.");
            return true;
        }

        if (zoneDefinition is HousingZoneDefinition)
        {
            ChatHelper.SendSystemMessage(invoker, $"{zoneDefinition.Name} is a housing zone. Use !house visit {zoneDefinition.Name}.");
            return true;
        }

        ulong? ownerId = zoneDefinition is WorldZoneDefinition ? null : invoker.Guid;

        if (!invoker.TeleportToZone(zoneDefinition.Id, ownerId, zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation))
            ChatHelper.SendSystemMessage(invoker, $"Could not enter {zoneDefinition.Name}.");

        return true;
    }
}
