using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class EventChatCommand : IChatCommand
{
    private const int MaxKeyLength = 32;

    private readonly IWorldEventManager _worldEventManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "event";
    public string Usage => "list | on <key> | off <key>";
    public string Description => "Turns GM events on or off. NPCs flagged with !npc event <key> only spawn while their event is on.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public EventChatCommand(IWorldEventManager worldEventManager, IChatCommandManager chatCommandManager)
    {
        _worldEventManager = worldEventManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 1 && args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var events = _worldEventManager.GetAll();

            ChatHelper.SendSystemMessage(invoker, events.Count == 0
                ? "No events yet. Flag an NPC with !npc event <key>, then !event on <key>."
                : $"Events: {string.Join(", ", events.OrderBy(x => x.Key).Select(x => $"{x.Key} ({(x.Value ? "on" : "off")})"))}");
            return true;
        }

        if (args.Length != 2)
            return false;

        bool active;

        if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
            active = true;
        else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
            active = false;
        else
            return false;

        var key = args[1].ToLowerInvariant();

        if (key.Length > MaxKeyLength)
        {
            ChatHelper.SendSystemMessage(invoker, $"Event keys are at most {MaxKeyLength} characters.");
            return true;
        }

        _worldEventManager.SetActive(key, active);

        _chatCommandManager.LogAction(this, invoker, active ? "On" : "Off", key, null);
        ChatHelper.SendSystemMessage(invoker, $"Event \"{key}\" is now {(active ? "on; its NPCs have spawned" : "off; its NPCs have despawned")} in every loaded world zone.");

        return true;
    }
}
