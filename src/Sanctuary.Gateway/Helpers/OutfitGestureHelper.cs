using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Helpers;

public static class OutfitGestureHelper
{
    private const int OutfitQuickChatMenu = 132;
    private const int NoAnimation = 0;
    private const int QuickChatLoopCount = -1;

    private const int GestureMilliseconds = 3000;
    private const int UnmatchedGestureDelayMilliseconds = 600;
    private const float WieldTypeGestureDelaySeconds = 0.3f;
    private const float TimelineGapSeconds = 0.01f;
    private const int LongestGestureMilliseconds = 4000;
    private const int GestureEndMarginMilliseconds = 100;
    private const int DancingGestureDelayMilliseconds = 100;

    private sealed record PendingGesture(GatewayConnection Connection, QuickChatSendChatToChannelPacket QuickChatSendChatToChannelPacket, int AnimationId);

    private sealed class PlayerGestures
    {
        public DateTimeOffset StartedAt;
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

    private static void Release(PendingGesture pendingGesture, bool wieldTypeChanged = false)
    {
        var player = pendingGesture.Connection.Player;

        if (!PendingGestures.TryRemove(new KeyValuePair<ulong, PendingGesture>(player.Guid, pendingGesture)))
            return;

        var delay = wieldTypeChanged ? WieldTypeGestureDelaySeconds : TimelineGapSeconds;
        var dancing = player.BoomboxDanceOwner != 0 && player.BoomboxDanceAnimation != 0;
        var dancingDelayMilliseconds = dancing ? DancingGestureDelayMilliseconds : default;
        var playerGestures = GestureStates.GetValue(player, _ => new PlayerGestures());

        lock (playerGestures)
        {
            var now = DateTimeOffset.UtcNow;

            if (now < playerGestures.StartedAt.AddMilliseconds(GestureMilliseconds))
                return;

            playerGestures.StartedAt = now.AddMilliseconds(dancingDelayMilliseconds).AddSeconds(delay);
        }

        player.OutfitGestureEndsAt = Environment.TickCount64 + dancingDelayMilliseconds
            + (long)TimeSpan.FromSeconds(delay + TimelineGapSeconds).TotalMilliseconds
            + LongestGestureMilliseconds + GestureEndMarginMilliseconds;

        if (dancing)
            _ = Task.Delay(dancingDelayMilliseconds).ContinueWith(_ => QueueGesture(player, pendingGesture.AnimationId, delay));
        else
            QueueGesture(player, pendingGesture.AnimationId, delay);

        var delayMilliseconds = dancingDelayMilliseconds + (int)TimeSpan.FromSeconds(delay).TotalMilliseconds;

        foreach (var visiblePlayer in player.VisiblePlayers.Values)
        {
            if (visiblePlayer.Ignores.Any(x => x.Guid == player.Guid))
                continue;

            visiblePlayer.SendTunneledDelayed(pendingGesture.QuickChatSendChatToChannelPacket, delayMilliseconds);
        }
    }

    private static void QueueGesture(Player player, int animationId, float delay)
    {
        EntityHelper.QueueAnimation(player, animationId, loopCount: QuickChatLoopCount, unknown6: false, unknown7: true, unknown8: false,
            delay: delay, recipients: [player]);
        EntityHelper.QueueAnimation(player, NoAnimation, loopCount: QuickChatLoopCount, unknown6: false, unknown7: true, unknown8: false,
            delay: TimelineGapSeconds, recipients: [player]);
    }
}
