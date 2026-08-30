using System.Numerics;
using System;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class NavNpcToChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "navnpcto";
    public string Usage => "<x> <y> <z> [direct]";
    public string Description => "Spawns a test NPC at your location and sends it walking to the given position. Optionally pass 'direct' to skip pathfinding.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public NavNpcToChatCommand(IChatCommandManager chatCommandManager)
    {
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length is not (3 or 4) ||
            !float.TryParse(args[0], out var x) ||
            !float.TryParse(args[1], out var y) ||
            !float.TryParse(args[2], out var z))
            return false;

        var direct = args.Length == 4 && args[3].Equals("direct", StringComparison.OrdinalIgnoreCase);

        if (!invoker.Zone.TryCreateNpc(null, out var npc))
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to spawn a test NPC.");
            return true;
        }

        npc.NameId = 437129;
        npc.ModelId = 3927;
        npc.Scale = 1f;
        npc.Disposition = 0;
        npc.HideNamePlate = false;
        npc.Speed = 6.25f;
        npc.Visible = true;

        npc.UpdatePosition(invoker.Position, invoker.Rotation);
        npc.MoveTo(new Vector3(x, y, z), direct);

        _chatCommandManager.LogAction(this, invoker, "Spawn NavTo NPC", null,
            $"guid={npc.Guid}, destination=({x},{y},{z}), direct={direct}");
        ChatHelper.SendSystemMessage(invoker,
            $"Spawned NPC {npc.Guid} moving to ({x:0.0}, {y:0.0}, {z:0.0}){(direct ? " directly" : "")}.");
        return true;
    }
}