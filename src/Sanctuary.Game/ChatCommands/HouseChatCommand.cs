using System;
using System.Linq;

using Sanctuary.Core.Helpers;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Housing;
using Sanctuary.Game.Resources.Definitions.Zones;

namespace Sanctuary.Game.ChatCommands;

public sealed class HouseChatCommand : IChatCommand
{
    private readonly IHouseManager _houseManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "house";
    public string Usage => "list | enter <seaside|blackspore> | visit <seaside|blackspore> <player> | leave";
    public string Description => "List, enter, visit, or leave a house.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public HouseChatCommand(IHouseManager houseManager, IResourceManager resourceManager)
    {
        _houseManager = houseManager;
        _resourceManager = resourceManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        return args[0].ToLowerInvariant() switch
        {
            "list" when args.Length == 1 => List(invoker),
            "enter" when args.Length == 2 => Enter(invoker, args[1]),
            "visit" when args.Length >= 3 => Visit(invoker, args[1], string.Join(' ', args[2..])),
            "leave" when args.Length == 1 => Leave(invoker),
            _ => false
        };
    }

    private bool List(Player invoker)
    {
        var characterId = GuidHelper.GetPlayerId(invoker.Guid);
        var zoneIds = _houseManager.GetOwnedHouses(characterId)
            .Select(house => house.ZoneDefinitionId)
            .ToHashSet();
        var names = _resourceManager.Zones.Values
            .OfType<HousingZoneDefinition>()
            .Where(definition => zoneIds.Contains(definition.Id))
            .OrderBy(definition => definition.DisplayName)
            .Select(definition => definition.DisplayName)
            .ToList();

        ChatHelper.SendSystemMessage(invoker, names.Count == 0
            ? "You do not own a house."
            : $"Owned houses: {string.Join(", ", names)}");

        return true;
    }

    private bool Enter(Player invoker, string commandName)
    {
        var definition = FindDefinition(commandName);

        if (definition is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown house: {commandName}");
            return true;
        }

        SendEnterResult(invoker, _houseManager.EnterOwnedHouse(invoker, definition.Id));
        return true;
    }

    private bool Visit(Player invoker, string commandName, string ownerName)
    {
        var definition = FindDefinition(commandName);

        if (definition is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown house: {commandName}");
            return true;
        }

        SendEnterResult(invoker, _houseManager.VisitHouse(invoker, definition.Id, ownerName));
        return true;
    }

    private bool Leave(Player invoker)
    {
        if (!_houseManager.LeaveHouse(invoker))
            ChatHelper.SendSystemMessage(invoker, "You are not in a house.");

        return true;
    }

    private HousingZoneDefinition? FindDefinition(string commandName)
    {
        return _resourceManager.Zones.Values
            .OfType<HousingZoneDefinition>()
            .FirstOrDefault(definition =>
                string.Equals(definition.CommandName, commandName, StringComparison.OrdinalIgnoreCase));
    }

    private static void SendEnterResult(Player player, EnterHouseResult result)
    {
        var message = result switch
        {
            EnterHouseResult.Success => null,
            EnterHouseResult.HouseNotFound => "That house is not available.",
            EnterHouseResult.NotAuthorized => "Only the owner and their friends can enter that house.",
            EnterHouseResult.UnsupportedSourceZone => "Return to the main world before entering a house.",
            EnterHouseResult.ZoneUnavailable => "The house instance could not be created.",
            EnterHouseResult.TransferFailed => "The house transfer failed.",
            _ => "The house could not be entered."
        };

        if (message is not null)
            ChatHelper.SendSystemMessage(player, message);
    }
}
