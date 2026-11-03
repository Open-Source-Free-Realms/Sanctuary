using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Routines;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Helpers;

public static class OutfitGestureHelper
{
    private const int OutfitQuickChatMenu = 132;
    private const int NoAnimation = 0;
    private const int QuickChatLoopCount = -1;

    private const string HoldRoutine = "OutfitGestureHold";
    private const string GestureRoutine = "OutfitGesture";

    private const double GestureSeconds = 3;
    private const double UnmatchedGestureDelaySeconds = 0.6;
    private const float WieldTypeGestureDelaySeconds = 0.3f;
    private const float TimelineGapSeconds = 0.01f;
    private const double LongestGestureSeconds = 4;
    private const double GestureEndMarginSeconds = 0.1;
    private const double DancingGestureDelaySeconds = 0.1;

    private sealed record PendingGesture(GatewayConnection Connection, QuickChatSendChatToChannelPacket QuickChatSendChatToChannelPacket, int AnimationId);

    private sealed class PlayerGestures
    {
        public bool IsCoolingDown;
    }

    private static readonly ConcurrentDictionary<ulong, PendingGesture> PendingGestures = new();
    private static readonly ConditionalWeakTable<Player, PlayerGestures> GestureStates = new();

    public static bool IsOutfitQuickChat(QuickChatDefinition quickChatDefinition) => quickChatDefinition.ParentId == OutfitQuickChatMenu;

    public static void Hold(GatewayConnection connection, QuickChatSendChatToChannelPacket quickChatSendChatToChannelPacket, int animationId)
    {
        var pendingGesture = new PendingGesture(connection, quickChatSendChatToChannelPacket, animationId);

        PendingGestures[connection.Player.Guid] = pendingGesture;

        connection.Player.Routines.SetRoutine(HoldRoutine, new DelayedRoutine(new InstantRoutine(() => Release(pendingGesture)), UnmatchedGestureDelaySeconds));
    }

    public static void OnEquipReply(GatewayConnection connection, int wieldType, bool wieldTypeChanged)
    {
        var guid = connection.Player.Guid;

        if (!PendingGestures.TryGetValue(guid, out var pendingGesture))
            return;

        connection.Player.Routines.Cancel(HoldRoutine);

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
        var playerGestures = GestureStates.GetValue(player, _ => new PlayerGestures());

        lock (playerGestures)
        {
            if (playerGestures.IsCoolingDown)
                return;

            playerGestures.IsCoolingDown = true;
        }

        player.IsPlayingOutfitGesture = true;

        var steps = new List<Routine>();

        if (dancing)
        {
            steps.Add(new WaitRoutine(DancingGestureDelaySeconds));
            steps.Add(new InstantRoutine(() => QueueGesture(player, pendingGesture.AnimationId, delay)));
        }
        else
        {
            QueueGesture(player, pendingGesture.AnimationId, delay);
        }

        steps.Add(new WaitRoutine(delay));
        steps.Add(new InstantRoutine(() => SendQuickChatToVisible(player, pendingGesture.QuickChatSendChatToChannelPacket)));
        steps.Add(new WaitRoutine(GestureSeconds));
        steps.Add(new InstantRoutine(() => EndCooldown(playerGestures)));
        steps.Add(new WaitRoutine(TimelineGapSeconds + LongestGestureSeconds + GestureEndMarginSeconds - GestureSeconds));
        steps.Add(new InstantRoutine(() => player.IsPlayingOutfitGesture = false));

        player.Routines.SetRoutine(GestureRoutine, new SequentialRoutine(steps));
    }

    private static void EndCooldown(PlayerGestures playerGestures)
    {
        lock (playerGestures)
        {
            playerGestures.IsCoolingDown = false;
        }
    }

    private static void SendQuickChatToVisible(Player player, QuickChatSendChatToChannelPacket quickChatSendChatToChannelPacket)
    {
        foreach (var visiblePlayer in player.VisiblePlayers.Values)
        {
            if (visiblePlayer.Ignores.Any(x => x.Guid == player.Guid))
                continue;

            visiblePlayer.SendTunneled(quickChatSendChatToChannelPacket);
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
