using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Routines;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
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

    private sealed record PendingGesture(Player Player, IZone Zone, QuickChatSendChatToChannelPacket QuickChatSendChatToChannelPacket, int AnimationId)
    {
        public bool IsInStartingZone => Player.Zone == Zone;
    }

    private sealed class PlayerGestures
    {
        public PendingGesture? Pending;
        private int _coolingDown;

        public bool TryTakePending(PendingGesture pendingGesture)
        {
            return ReferenceEquals(Interlocked.CompareExchange(ref Pending, null, pendingGesture), pendingGesture);
        }

        public bool TryStartCooldown()
        {
            return Interlocked.CompareExchange(ref _coolingDown, 1, 0) == 0;
        }

        public void EndCooldown()
        {
            Interlocked.Exchange(ref _coolingDown, 0);
        }
    }

    private static readonly ConditionalWeakTable<Player, PlayerGestures> GestureStates = new();

    public static bool IsOutfitQuickChat(QuickChatDefinition quickChatDefinition) => quickChatDefinition.ParentId == OutfitQuickChatMenu;

    public static void Hold(GatewayConnection connection, QuickChatSendChatToChannelPacket quickChatSendChatToChannelPacket, int animationId)
    {
        var player = connection.Player;
        var pendingGesture = new PendingGesture(player, player.Zone, quickChatSendChatToChannelPacket, animationId);

        GestureStates.GetValue(player, _ => new PlayerGestures()).Pending = pendingGesture;

        player.Routines.SetRoutine(HoldRoutine, new DelayedRoutine(new InstantRoutine(() => Release(pendingGesture)), UnmatchedGestureDelaySeconds));
    }

    public static void OnEquipReply(GatewayConnection connection, ItemClassDefinition? itemClass, int previousWieldType)
    {
        var player = connection.Player;
        var wieldTypeChanged = player.GetWieldType() != previousWieldType;

        if (wieldTypeChanged)
            player.SendActiveWieldType();

        if (!GestureStates.TryGetValue(player, out var playerGestures) || playerGestures.Pending is not { } pendingGesture)
            return;

        player.Routines.Cancel(HoldRoutine);

        Release(pendingGesture, wieldTypeChanged, itemClass?.WieldType is null or 0 || !wieldTypeChanged);
    }

    private static void Release(PendingGesture pendingGesture, bool wieldTypeChanged = false, bool play = true)
    {
        var player = pendingGesture.Player;
        var playerGestures = GestureStates.GetValue(player, _ => new PlayerGestures());

        if (!playerGestures.TryTakePending(pendingGesture) || !play || !pendingGesture.IsInStartingZone || !playerGestures.TryStartCooldown())
            return;

        var delay = wieldTypeChanged ? WieldTypeGestureDelaySeconds : TimelineGapSeconds;
        var dancing = player.BoomboxDanceOwner != 0 && player.BoomboxDanceAnimation != 0;

        player.IsPlayingOutfitGesture = true;

        var steps = new List<Routine>();

        if (dancing)
        {
            steps.Add(new WaitRoutine(DancingGestureDelaySeconds));
            steps.Add(new InstantRoutine(() => QueueGesture(pendingGesture, delay)));
        }
        else
        {
            QueueGesture(pendingGesture, delay);
        }

        steps.Add(new WaitRoutine(delay));
        steps.Add(new InstantRoutine(() => SendQuickChatToVisible(pendingGesture)));
        steps.Add(new WaitRoutine(GestureSeconds));
        steps.Add(new InstantRoutine(playerGestures.EndCooldown));
        steps.Add(new WaitRoutine(TimelineGapSeconds + LongestGestureSeconds + GestureEndMarginSeconds - GestureSeconds));
        steps.Add(new InstantRoutine(() => player.IsPlayingOutfitGesture = false));

        player.Routines.SetRoutine(GestureRoutine, new SequentialRoutine(steps));
    }

    private static void SendQuickChatToVisible(PendingGesture pendingGesture)
    {
        if (!pendingGesture.IsInStartingZone)
            return;

        var player = pendingGesture.Player;

        foreach (var visiblePlayer in player.VisiblePlayers.Values)
        {
            if (visiblePlayer.Ignores.Any(x => x.Guid == player.Guid))
                continue;

            visiblePlayer.SendTunneled(pendingGesture.QuickChatSendChatToChannelPacket);
        }
    }

    private static void QueueGesture(PendingGesture pendingGesture, float delay)
    {
        if (!pendingGesture.IsInStartingZone)
            return;

        var player = pendingGesture.Player;

        EntityHelper.QueueAnimation(player, pendingGesture.AnimationId, loopCount: QuickChatLoopCount, easeOutLoopingAnimations: false, useTransitions: true, useAnimationPriority: false,
            delay: delay, recipients: [player]);
        EntityHelper.QueueAnimation(player, NoAnimation, loopCount: QuickChatLoopCount, easeOutLoopingAnimations: false, useTransitions: true, useAnimationPriority: false,
            delay: TimelineGapSeconds, recipients: [player]);
    }
}
