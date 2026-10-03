using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public static class EntityHelper
{
    public static void RemovePlayerGracefully(IEntity entity, bool animate = false, int delay = 0,
        int effectDelay = 0, int compositeEffectId = 0, int duration = 1000, bool sendToSelf = false,
        IEnumerable<Player>? recipients = null)
    {
        var visiblePlayers = new HashSet<Player>(recipients ?? entity.VisiblePlayers.Values);

        if (sendToSelf && entity is Player player)
            visiblePlayers.Add(player);

        var playerUpdatePacketRemovePlayerGracefully = new PlayerUpdatePacketRemovePlayerGracefully
        {
            Guid = entity.Guid,
            Animate = animate,
            Delay = delay,
            EffectDelay = effectDelay,
            CompositeEffectId = compositeEffectId,
            Duration = duration
        };

        var data = Player.SerializeTunneled(playerUpdatePacketRemovePlayerGracefully);

        foreach (var visiblePlayer in visiblePlayers)
        {
            visiblePlayer.SendSerialized(data);

            if (entity is Npc)
                visiblePlayer.VisibleNpcs.TryRemove(entity.Guid, out _);
            else if (entity is Player)
                visiblePlayer.VisiblePlayers.TryRemove(entity.Guid, out _);
        }

        // The graceful packet already removes the entity from its viewers.
        entity.VisiblePlayers.Clear();
        entity.Dispose();
    }
}
