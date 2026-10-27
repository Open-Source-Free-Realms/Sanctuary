using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Referee tools for zone areas: the terrain tile environments, POI and chat areas players are placed in.
/// </summary>
public class AreaChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "area";

    public string Usage => "here | list | goto <areaId> | client <areaId> on|off";

    public string Description => "Shows which tile, environment and area you're in, lists the zone's areas, teleports to an area's point of interest, and toggles client areas for everyone in the zone.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public AreaChatCommand(IChatCommandManager chatCommandManager, IResourceManager resourceManager)
    {
        _chatCommandManager = chatCommandManager;
        _resourceManager = resourceManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        var subCommand = args.Length == 0 ? "here" : args[0].ToLowerInvariant();

        return subCommand switch
        {
            "here" => HandleHere(invoker),
            "list" => HandleList(invoker),
            "goto" => HandleGoto(invoker, args[1..]),
            "client" => HandleClient(invoker, args[1..]),
            _ => false
        };
    }

    private static string GetAreaName(ZoneAreaDefinition area) =>
        GameNameHelper.Get(area.NameId) ?? GameNameHelper.Get(area.ChatAreaNameId) ?? $"Area {area.Id}";

    private string GetEnvironmentName(IZone zone, int environmentId)
    {
        if (environmentId == ZoneEnvironmentDefinition.OutOfBoundsEnvironmentId)
            return "out of bounds";

        return _resourceManager.ZoneEnvironments.TryGetValue(zone.Name, out var environments)
            && environments.Environments.TryGetValue(environmentId, out var name)
                ? name
                : "unknown";
    }

    private bool HandleHere(Player invoker)
    {
        var zone = invoker.Zone;
        var tile = zone.GetTileFromPosition(invoker.Position);

        ChatHelper.SendSystemMessage(invoker, $"{zone.Name} ({zone.DefinitionId}) at {invoker.Position.X:0.0}, {invoker.Position.Y:0.0}, {invoker.Position.Z:0.0}");
        ChatHelper.SendSystemMessage(invoker, $"  Tile {tile.Longitude}, {tile.Latitude}, environment {tile.EnvironmentId} ({GetEnvironmentName(zone, tile.EnvironmentId)})");

        if (zone is WorldZone worldZone)
        {
            var area = worldZone.GetZoneArea(invoker.Position, tile);

            ChatHelper.SendSystemMessage(invoker, area is null
                ? "  Not inside any area."
                : $"  Area {area.Id} {GetAreaName(area)}, POI {area.PointOfInterestId}, chat area {area.ChatAreaNameId}");
        }

        ChatHelper.SendSystemMessage(invoker, $"  Client state: POI {invoker.PointOfInterestId}, chat area {invoker.ChatAreaNameId}");
        return true;
    }

    private bool TryGetWorldZoneDefinition(Player invoker, out WorldZoneDefinition definition)
    {
        definition = null!;

        if (_resourceManager.Zones.TryGetValue(invoker.Zone.DefinitionId, out var zoneDefinition) && zoneDefinition is WorldZoneDefinition worldZoneDefinition)
        {
            definition = worldZoneDefinition;
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, "This zone has no areas.");
        return false;
    }

    private bool HandleList(Player invoker)
    {
        if (!TryGetWorldZoneDefinition(invoker, out var definition))
            return true;

        ChatHelper.SendSystemMessage(invoker, $"{definition.AreaDefinitions.Count} area(s) in {invoker.Zone.Name}:");

        foreach (var area in definition.AreaDefinitions.OrderBy(area => area.Id))
        {
            var placement = area.Tiles.Count > 0
                ? $"{area.Tiles.Count} tile(s)"
                : area.EnvironmentIds.Count > 0
                    ? $"environments {string.Join(",", area.EnvironmentIds)}"
                    : area.Shape.ToString();

            ChatHelper.SendSystemMessage(invoker, $"  {area.Id} {GetAreaName(area)} - {placement}, POI {area.PointOfInterestId}");
        }

        if (definition.EnabledClientAreaIds.Count > 0)
            ChatHelper.SendSystemMessage(invoker, $"  Client areas on by default: {string.Join(", ", definition.EnabledClientAreaIds)}");

        return true;
    }

    private bool HandleGoto(Player invoker, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var areaId))
            return false;

        if (!TryGetWorldZoneDefinition(invoker, out var definition))
            return true;

        var area = definition.AreaDefinitions.FirstOrDefault(x => x.Id == areaId);

        if (area is null || !_resourceManager.PointOfInterests.TryGetValue(area.PointOfInterestId, out var pointOfInterest))
        {
            ChatHelper.SendSystemMessage(invoker, $"Area {areaId} has no point of interest to go to.");
            return true;
        }

        invoker.UpdatePosition(pointOfInterest.Position, invoker.Rotation);
        invoker.SendTunneled(new ClientUpdatePacketUpdateLocation
        {
            Position = pointOfInterest.Position,
            Rotation = invoker.Rotation,
            Teleport = true
        });

        _chatCommandManager.LogAction(this, invoker, "Goto area", null, areaId.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Teleported to {GetAreaName(area)}.");
        return true;
    }

    private bool HandleClient(Player invoker, string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out var areaId))
            return false;

        var enabled = args[1].ToLowerInvariant() switch
        {
            "on" => true,
            "off" => false,
            _ => (bool?)null
        };

        if (enabled is null)
            return false;

        invoker.Zone.SetClientAreaEnabled(areaId, enabled.Value);

        _chatCommandManager.LogAction(this, invoker, enabled.Value ? "Enable client area" : "Disable client area", null, areaId.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Client area {areaId} is now {(enabled.Value ? "on" : "off")} for everyone in {invoker.Zone.Name}.");
        return true;
    }
}
