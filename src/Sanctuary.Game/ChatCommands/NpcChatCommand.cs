using System.Linq;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class NpcChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "npc";

    public string Usage => "spawn <definitionId> | despawn [guid]";

    public string Description => "Spawn an NPC from a definition at your location, or despawn the nearest (or specified) NPC.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public NpcChatCommand(IChatCommandManager chatCommandManager, IResourceManager resourceManager)
    {
        _chatCommandManager = chatCommandManager;
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

        switch (args[0].ToLowerInvariant())
        {
            case "spawn":
                return Spawn(invoker, args[1..]);
            case "despawn":
                return Despawn(invoker, args[1..]);
            default:
                return false;
        }
    }

    private bool Spawn(Player invoker, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var definitionId))
            return false;

        if (!_resourceManager.Npcs.TryGetValue(definitionId, out var definition))
        {
            ChatHelper.SendSystemMessage(invoker, $"No NPC definition with id {definitionId} was found.");
            return true;
        }

        if (!invoker.Zone.TryCreateNpc(null, definition, out var npc))
        {
            ChatHelper.SendSystemMessage(invoker, "The NPC could not be spawned.");
            return true;
        }

        npc.UpdatePosition(invoker.Position, invoker.Rotation);

        LogAction(invoker, "Spawn NPC", $"#{definitionId}", $"guid={npc.Guid}, position={npc.Position}");
        ChatHelper.SendSystemMessage(invoker, $"Spawned NPC {npc.Name} (def #{definitionId} guid {npc.Guid}).");
        return true;
    }

    private bool Despawn(Player invoker, string[] args)
    {
        if (args.Length > 1)
            return false;

        Npc? target;

        if (args.Length == 1)
        {
            if (!ulong.TryParse(args[0], out var guid))
                return false;

            if (!invoker.Zone.TryGetNpc(guid, out target) || !IsDespawnable(target))
            {
                ChatHelper.SendSystemMessage(invoker, $"No NPC with guid {guid} was found in your zone.");
                return true;
            }
        }
        else
        {
            target = FindNearestNpc(invoker);

            if (target is null)
            {
                ChatHelper.SendSystemMessage(invoker, "There are no NPCs in your zone to despawn.");
                return true;
            }
        }

        var removedGuid = target.Guid;
        var removedName = target.Name;
        target.Dispose();

        LogAction(invoker, "Despawn NPC", $"guid={removedGuid}");
        ChatHelper.SendSystemMessage(invoker, $"Despawned NPC {removedName} (guid {removedGuid}).");
        return true;
    }

    private static Npc? FindNearestNpc(Player invoker)
    {
        return invoker.Zone.Npcs
            .Where(IsDespawnable)
            .MinBy(npc => Vector4.DistanceSquared(invoker.Position, npc.Position));
    }

    private static bool IsDespawnable(Npc npc)
    {
        // Mounts are owned by players and collection nodes are managed by their pools,
        // so neither should be removed directly through this command.
        return npc is not Mount and not CollectionNode;
    }
}
