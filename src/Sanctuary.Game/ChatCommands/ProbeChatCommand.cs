using System.Collections.Concurrent;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class ProbeChatCommand : IChatCommand
{
    private static readonly ConcurrentDictionary<ulong, int> _activeTags = new();

    public string KeyWord => "probe";
    public string Usage => "<compositeEffectId> | clear";
    public string Description => "Attaches a composite effect to you as a persistent tag (like a real aura), for previewing effect ids. 'clear' removes it.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1)
            return false;

        if (_activeTags.TryRemove(invoker.Guid, out var previousTagId))
            invoker.SendTunneledToVisible(new PlayerUpdatePacketRemoveEffectTagCompositeEffect { Guid = invoker.Guid, TagId = previousTagId }, true);

        if (args[0].Equals("clear", System.StringComparison.OrdinalIgnoreCase))
        {
            ChatHelper.SendSystemMessage(invoker, "Probe effect cleared.");
            return true;
        }

        if (!int.TryParse(args[0], out var effectId))
            return false;

        var tagId = EffectTagIdGenerator.Next();
        _activeTags[invoker.Guid] = tagId;

        invoker.SendTunneledToVisible(new PlayerUpdatePacketAddEffectTagCompositeEffect
        {
            Guid = invoker.Guid,
            TagId = tagId,
            CompositeEffectId = effectId,
            SourceGuid = invoker.Guid
        }, true);

        ChatHelper.SendSystemMessage(invoker, $"Attached composite effect {effectId}. Use !probe clear to remove it.");

        return true;
    }
}
