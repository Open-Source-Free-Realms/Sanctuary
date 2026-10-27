using System.Globalization;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.ChatCommands;

public class PlaceChatCommand : IChatCommand
{
    private const float DefaultRadius = 10.0f;

    private readonly IChatCommandManager _chatCommandManager;
    private readonly IHousingManager _housingManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "place";
    public string Usage => "mode <item_id> [tint_id] [scale] | off | item <item_id> [tint_id] [scale] | remove [radius] | list [radius]";
    public string Description => "GM world placement. 'mode' opens the housing placement tool in the world, 'item' drops a prop where you stand.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public PlaceChatCommand(IChatCommandManager chatCommandManager, IHousingManager housingManager, IResourceManager resourceManager)
    {
        _chatCommandManager = chatCommandManager;
        _housingManager = housingManager;
        _resourceManager = resourceManager;
    }

    private void LogAction(Player invoker, string action, string? targetName = null, string? detail = null)
    {
        _chatCommandManager.LogAction(this, invoker, action, targetName, detail);
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 1)
            return false;

        if (invoker.Zone is not WorldZone worldZone)
        {
            ChatHelper.SendSystemMessage(invoker, "World placement only works in a world zone.");
            return true;
        }

        return args[0].ToLowerInvariant() switch
        {
            "mode" => HandleMode(invoker, worldZone, args[1..]),
            "off" => HandleOff(invoker, worldZone),
            "item" => HandleItem(invoker, worldZone, args[1..]),
            "remove" => HandleRemove(invoker, worldZone, args[1..]),
            "list" => HandleList(invoker, worldZone, args[1..]),
            _ => false
        };
    }

    private bool HandleMode(Player invoker, WorldZone worldZone, string[] args)
    {
        if (!TryParseItem(invoker, args, out var itemDefinitionId, out var tintId, out var scale))
            return false;

        if (!worldZone.TryBeginPlacement(invoker, itemDefinitionId, tintId, scale))
        {
            ChatHelper.SendSystemMessage(invoker, $"Item {itemDefinitionId} can't be placed.");
            return true;
        }

        LogAction(invoker, "Begin World Placement", $"#{itemDefinitionId}", $"tint={tintId}, scale={scale}");
        ChatHelper.SendSystemMessage(invoker, $"Placing {GetItemName(itemDefinitionId)}. Click to place, '!{KeyWord} off' to stop.");
        return true;
    }

    private bool HandleOff(Player invoker, WorldZone worldZone)
    {
        if (!worldZone.EndPlacement(invoker))
        {
            ChatHelper.SendSystemMessage(invoker, "You're not in placement mode.");
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, "Left placement mode.");
        return true;
    }

    private bool HandleItem(Player invoker, WorldZone worldZone, string[] args)
    {
        if (!TryParseItem(invoker, args, out var itemDefinitionId, out var tintId, out var scale))
            return false;

        if (!worldZone.TryPlaceWorldFixture(invoker, itemDefinitionId, tintId, invoker.Position, invoker.Rotation, scale, out var npc))
        {
            ChatHelper.SendSystemMessage(invoker, $"Item {itemDefinitionId} can't be placed.");
            return true;
        }

        LogAction(invoker, "Place World Fixture", $"#{itemDefinitionId}", $"guid={npc.Guid}, position={npc.Position}");
        ChatHelper.SendSystemMessage(invoker, $"Placed {GetItemName(itemDefinitionId)} (guid {npc.Guid}).");
        return true;
    }

    private bool HandleRemove(Player invoker, WorldZone worldZone, string[] args)
    {
        if (!TryParseRadius(args, out var radius))
            return false;

        if (!worldZone.TryGetNearestWorldFixture(invoker.Position, radius, out var npc)
            || !worldZone.TryRemoveWorldFixture(npc.Guid, out var fixture))
        {
            ChatHelper.SendSystemMessage(invoker, $"No placed props within {radius} units.");
            return true;
        }

        LogAction(invoker, "Remove World Fixture", $"#{fixture.Definition}", $"guid={npc.Guid}, id={fixture.Id}");
        ChatHelper.SendSystemMessage(invoker, $"Removed {GetItemName(fixture.Definition)} (guid {npc.Guid}).");
        return true;
    }

    private bool HandleList(Player invoker, WorldZone worldZone, string[] args)
    {
        if (!TryParseRadius(args, out var radius))
            return false;

        var nearby = worldZone.GetWorldFixtures()
            .Where(x => Vector4.Distance(invoker.Position, x.Npc.Position) <= radius)
            .OrderBy(x => Vector4.DistanceSquared(invoker.Position, x.Npc.Position))
            .ToList();

        if (nearby.Count == 0)
        {
            ChatHelper.SendSystemMessage(invoker, $"No placed props within {radius} units.");
            return true;
        }

        foreach (var (npc, fixture) in nearby)
        {
            var distance = Vector4.Distance(invoker.Position, npc.Position);

            ChatHelper.SendSystemMessage(invoker, $"{GetItemName(fixture.Definition)} (item {fixture.Definition}) guid {npc.Guid}, {distance:0.0} away");
        }

        return true;
    }

    private bool TryParseItem(Player invoker, string[] args, out int itemDefinitionId, out int tintId, out float scale)
    {
        tintId = 0;
        scale = 1.0f;

        if (args.Length < 1 || args.Length > 3 || !int.TryParse(args[0], out itemDefinitionId))
        {
            itemDefinitionId = 0;
            return false;
        }

        if (args.Length >= 2 && !int.TryParse(args[1], out tintId))
            return false;

        if (args.Length == 3 && (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out scale) || scale <= 0.0f))
            return false;

        if (!_housingManager.TryGetFixtureDefinition(itemDefinitionId, out _))
        {
            ChatHelper.SendSystemMessage(invoker, $"Item {itemDefinitionId} isn't a placeable prop.");
            return false;
        }

        return true;
    }

    private static bool TryParseRadius(string[] args, out float radius)
    {
        radius = DefaultRadius;

        if (args.Length == 0)
            return true;

        return args.Length == 1
            && float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out radius)
            && radius > 0.0f;
    }

    private string GetItemName(int itemDefinitionId)
    {
        return _resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition) && !string.IsNullOrEmpty(itemDefinition.ModelName)
            ? itemDefinition.ModelName
            : $"item {itemDefinitionId}";
    }
}
