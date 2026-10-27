using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class GodModeChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "godmode";
    public string Usage => "[on|off]";
    public string Description => "Toggles referee editing: cursors, glow and GM menus on every NPC and placed prop.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public GodModeChatCommand(IChatCommandManager chatCommandManager)
    {
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        bool enable;

        if (args.Length == 0)
            enable = !invoker.GodMode;
        else if (args.Length == 1 && args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
            enable = true;
        else if (args.Length == 1 && args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
            enable = false;
        else
            return false;

        if (enable == invoker.GodMode)
        {
            ChatHelper.SendSystemMessage(invoker, $"God mode is already {(enable ? "on" : "off")}.");
            return true;
        }

        invoker.GodMode = enable;

        if (!enable)
            invoker.SelectedNpcGuid = null;

        // The cursor and glow ride on the NPC add packets, so re-send everything in view.
        var visibleNpcs = invoker.VisibleNpcs.Values.ToList();

        invoker.OnRemoveVisibleNpcs(visibleNpcs);
        invoker.OnAddVisibleNpcs(visibleNpcs);

        _chatCommandManager.LogAction(this, invoker, enable ? "On" : "Off", null, null);
        ChatHelper.SendSystemMessage(invoker, enable
            ? "God mode on: click any NPC or placed prop to edit it."
            : "God mode off: you see the world as players do.");

        return true;
    }
}
