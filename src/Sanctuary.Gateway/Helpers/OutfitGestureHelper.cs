using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Helpers;

public static class OutfitGestureHelper
{
    private const int OutfitQuickChatMenu = 132;
    private const int NoAnimation = 0;
    private const int QuickChatLoopCount = -1;

    private const float GestureSeconds = 3f;
    private const int UnmatchedGestureDelayMilliseconds = 600;
    private const float GestureQueueDelaySeconds = 0.3f;
    private const float MarkerDelaySeconds = 0.01f;

    private sealed record PendingGesture(GatewayConnection Connection, QuickChatSendChatToChannelPacket QuickChatSendChatToChannelPacket, int AnimationId);

    private sealed class PlayerGestures
    {
        public DateTimeOffset StartedAt;
        public IZone? Zone;
        public readonly HashSet<int> PlayedAnimationIds = [];
    }

    private static readonly ConcurrentDictionary<ulong, PendingGesture> PendingGestures = new();
    private static readonly ConditionalWeakTable<Player, PlayerGestures> GestureStates = new();

    public static bool IsOutfitQuickChat(QuickChatDefinition quickChatDefinition) => quickChatDefinition.ParentId == OutfitQuickChatMenu;

    public static void Hold(GatewayConnection connection, QuickChatSendChatToChannelPacket quickChatSendChatToChannelPacket, int animationId)
    {
        var pendingGesture = new PendingGesture(connection, quickChatSendChatToChannelPacket, animationId);

        PendingGestures[connection.Player.Guid] = pendingGesture;

        _ = Task.Delay(UnmatchedGestureDelayMilliseconds).ContinueWith(_ => Release(pendingGesture));
    }

    public static void OnEquipReply(GatewayConnection connection, int wieldType, bool wieldTypeChanged)
    {
        var guid = connection.Player.Guid;

        if (!PendingGestures.TryGetValue(guid, out var pendingGesture))
            return;

        if (wieldType != 0 && wieldTypeChanged)
            PendingGestures.TryRemove(new KeyValuePair<ulong, PendingGesture>(guid, pendingGesture));
        else
            Release(pendingGesture, wieldTypeChanged);
    }

    private static void Release(PendingGesture pendingGesture, bool queue = false)
    {
        var player = pendingGesture.Connection.Player;

        if (!PendingGestures.TryRemove(new KeyValuePair<ulong, PendingGesture>(player.Guid, pendingGesture)))
            return;

        var playerGestures = GestureStates.GetValue(player, _ => new PlayerGestures());
        bool firstPlay;

        lock (playerGestures)
        {
            var now = DateTimeOffset.UtcNow;

            if (now < playerGestures.StartedAt.AddSeconds(GestureSeconds))
                return;

            playerGestures.StartedAt = now;

            if (playerGestures.Zone != player.Zone)
            {
                playerGestures.Zone = player.Zone;
                playerGestures.PlayedAnimationIds.Clear();
            }

            firstPlay = playerGestures.PlayedAnimationIds.Add(pendingGesture.AnimationId);
        }

        var delay = queue ? GestureQueueDelaySeconds : default;

        EntityHelper.QueueAnimation(player, pendingGesture.AnimationId, loopCount: QuickChatLoopCount, unknown6: false, unknown7: true, unknown8: false,
            delay: delay, recipients: [player]);

        if (firstPlay)
            EntityHelper.QueueAnimation(player, NoAnimation, loopCount: QuickChatLoopCount, unknown6: false, unknown7: true, unknown8: false,
                endTime: delay + GestureSeconds, recipients: [player]);

        EntityHelper.QueueAnimation(player, NoAnimation, loopCount: QuickChatLoopCount, unknown6: false, unknown7: true, unknown8: false,
            delay: MarkerDelaySeconds, recipients: [player]);

        var delayMilliseconds = (int)TimeSpan.FromSeconds(delay).TotalMilliseconds;

        foreach (var visiblePlayer in player.VisiblePlayers.Values)
        {
            if (visiblePlayer.Ignores.Any(x => x.Guid == player.Guid))
                continue;

            visiblePlayer.SendTunneledDelayed(pendingGesture.QuickChatSendChatToChannelPacket, delayMilliseconds);
        }
    }
}
