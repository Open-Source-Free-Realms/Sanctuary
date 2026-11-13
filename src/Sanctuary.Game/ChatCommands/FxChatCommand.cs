using System;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class EffectsChatCommand : IChatCommand
{
    public string KeyWord => "fx";
    public string Usage => "<compositeEffectId>";
    public string Description => "Spawns a composite effect ontop of you. Find composite effects at ActorCompositeEffectDefinitions.xml in Resources.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var effectId))
            return false;

        invoker.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = invoker.Guid,
            CompositeEffectId = effectId,
            Position = invoker.Position
        }, true);

        ChatHelper.SendSystemMessage(invoker, $"Played effect {effectId}.");
        return true;
    }
}