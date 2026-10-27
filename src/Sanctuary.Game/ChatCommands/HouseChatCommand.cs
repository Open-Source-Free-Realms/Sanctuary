using System.Linq;

using Sanctuary.Core.Helpers;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.ChatCommands;

public class HouseChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IHousingManager _housingManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "house";
    public string Usage => "list | enter [index] | leave | visit <player name> | grant <house_id> | unbound | rotate yaw|pitch|roll <degrees>";
    public string Description => "Lists, enters, and leaves your houses. 'grant' and 'unbound' (place anywhere in the current house) require admin.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public HouseChatCommand(IZoneManager zoneManager, IHousingManager housingManager, IResourceManager resourceManager)
    {
        _zoneManager = zoneManager;
        _housingManager = housingManager;
        _resourceManager = resourceManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "list":
                return HandleList(invoker);

            case "enter":
                return HandleEnter(invoker, args);

            case "leave":
                if (!_housingManager.TryLeaveHouse(invoker))
                    ChatHelper.SendSystemMessage(invoker, "You're not in a house.");
                return true;

            case "visit":
                return HandleVisit(invoker, args);

            case "grant":
                return HandleGrant(invoker, args);

            case "unbound":
                return HandleUnbound(invoker);

            case "rotate":
                return HandleRotate(invoker, args);

            default:
                return false;
        }
    }

    private bool HandleList(Player invoker)
    {
        _housingManager.ProvisionHousesFromItems(invoker);

        var houses = _housingManager.GetHouses(GuidHelper.GetPlayerId(invoker.Guid));

        if (houses.Count == 0)
        {
            ChatHelper.SendSystemMessage(invoker, "You don't own any houses.");
            return true;
        }

        for (var i = 0; i < houses.Count; i++)
        {
            var house = houses[i];

            var name = _resourceManager.Zones.TryGetValue(_resourceManager.Houses.TryGetValue(house.Definition, out var definition) ? definition.ZoneId : 0, out var zone)
                ? zone.Name
                : $"house {house.Definition}";

            ChatHelper.SendSystemMessage(invoker, $"[{i + 1}] {house.Name ?? name} - {house.Fixtures.Count} fixture(s){(house.IsLocked ? ", locked" : string.Empty)}");
        }

        return true;
    }

    private bool HandleEnter(Player invoker, string[] args)
    {
        _housingManager.ProvisionHousesFromItems(invoker);

        var houses = _housingManager.GetHouses(GuidHelper.GetPlayerId(invoker.Guid));

        var index = 1;

        if (args.Length > 1 && !int.TryParse(args[1], out index))
            return false;

        if (index < 1 || index > houses.Count)
        {
            ChatHelper.SendSystemMessage(invoker, "You don't own a house with that index. Use '!house list'.");
            return true;
        }

        _housingManager.TryEnterHouse(invoker, GuidHelper.GetHouseGuid(houses[index - 1].Id));

        return true;
    }

    private bool HandleVisit(Player invoker, string[] args)
    {
        if (args.Length < 2)
            return false;

        var name = string.Join(' ', args.Skip(1));

        if (!_zoneManager.TryGetPlayer(name, out var target))
        {
            ChatHelper.SendSystemMessage(invoker, $"Player \"{name}\" is not online.");
            return true;
        }

        _housingManager.TryVisitPlayer(invoker, target.Guid);

        return true;
    }

    private bool HandleUnbound(Player invoker)
    {
        if (invoker.ChatCommandRole < ChatCommandRole.Admin)
        {
            ChatHelper.SendSystemMessage(invoker, "You do not have permission to use this command.");
            return true;
        }

        if (invoker.Zone is not HousingZone housingZone)
        {
            ChatHelper.SendSystemMessage(invoker, "You're not in a house.");
            return true;
        }

        housingZone.SetUnbound(!housingZone.IsUnbound);

        ChatHelper.SendSystemMessage(invoker, housingZone.IsUnbound
            ? "Fixtures can now be placed anywhere in this house."
            : "Fixtures are limited to the lot again.");

        return true;
    }

    private bool HandleRotate(Player invoker, string[] args)
    {
        if (args.Length < 3 || !float.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var degrees))
            return false;

        var axis = args[1].ToLowerInvariant() switch
        {
            "yaw" or "y" => 'y',
            "pitch" or "p" => 'p',
            "roll" or "r" => 'r',
            _ => '\0'
        };

        if (axis == '\0')
            return false;

        if (invoker.Zone is not HousingZone housingZone)
        {
            ChatHelper.SendSystemMessage(invoker, "You're not in a house.");
            return true;
        }

        housingZone.RotateLastFixture(invoker, axis, degrees, out var message);
        ChatHelper.SendSystemMessage(invoker, message);
        return true;
    }

    private bool HandleGrant(Player invoker, string[] args)
    {
        if (invoker.ChatCommandRole < ChatCommandRole.Admin)
        {
            ChatHelper.SendSystemMessage(invoker, "You do not have permission to use this command.");
            return true;
        }

        if (args.Length != 2 || !int.TryParse(args[1], out var houseDefinitionId))
            return false;

        if (!_resourceManager.Houses.TryGetValue(houseDefinitionId, out var houseDefinition))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown house id {houseDefinitionId}.");
            return true;
        }

        if (!_housingManager.TryGrantHouse(invoker, houseDefinition, out _))
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to grant house.");
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"Granted house {houseDefinitionId}. Use '!house list'.");

        return true;
    }
}
