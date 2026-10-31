using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    private const float GestureQueueDelaySeconds = 0.3f;
    private const float MarkerDelaySeconds = 0.01f;

    private sealed record PendingGesture(GatewayConnection Connection, QuickChatSendChatToChannelPacket QuickChatSendChatToChannelPacket, int AnimationId);

    private static readonly ConcurrentDictionary<ulong, PendingGesture> PendingGestures = new();
    private static readonly ConcurrentDictionary<ulong, DateTimeOffset> GestureStartTimes = new();

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

        var now = DateTimeOffset.UtcNow;

        if (GestureStartTimes.TryGetValue(player.Guid, out var gestureStartTime) && now < gestureStartTime.AddMilliseconds(GestureMilliseconds))
            return;

        GestureStartTimes[player.Guid] = now;

        EntityHelper.QueueAnimation(player, pendingGesture.AnimationId, loopCount: QuickChatLoopCount, unknown6: false, unknown7: true, unknown8: false,
            delay: queue ? GestureQueueDelaySeconds : default, recipients: [player]);
        EntityHelper.QueueAnimation(player, NoAnimation, loopCount: QuickChatLoopCount, unknown6: false, unknown7: true, unknown8: false,
            delay: MarkerDelaySeconds, recipients: [player]);

        player.SendTunneledToVisible(pendingGesture.QuickChatSendChatToChannelPacket);
    }
}
