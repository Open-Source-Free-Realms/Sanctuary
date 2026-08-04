using System.Collections.Concurrent;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

using static Sanctuary.Gateway.Handlers.AbilityPacketClientRequestStartAbilityHandler;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Split out of the old handler's big if-chain - same logic, just its own class now.
public sealed class SillyStringAbility : IConsumableAbility
{
    // Who each player last sprayed, so back-to-back cans don't just soak the same nearest victim over
    // and over - spread it around when there's anyone else to spread it to.
    private static readonly ConcurrentDictionary<ulong, ulong> _lastSillyStringTarget = new();

    public bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.PartyFavors.ContainsKey(itemDefinition.Id);

    public bool Handle(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        _resourceManager.Consumables.PartyFavors.TryGetValue(itemDefinition.Id, out var favor);

        var player = connection.Player;
        var zone = player.Zone;

        if (IsOnCooldown(player.Guid, itemDefinition.Id))
            return SendFailure(connection);

        // Not an aimable ability, so no selected-target honoring. Skip whoever was sprayed last time,
        // unless they're the only one around.
        _lastSillyStringTarget.TryGetValue(player.Guid, out var lastTargetGuid);

        var target = AbilityTargeting.FindNearestPlayer(zone, player, favor!.Range, lastTargetGuid)
            ?? AbilityTargeting.FindNearestPlayer(zone, player, favor.Range);

        if (target is null)
            return SendFailure(connection); // nobody nearby to spray - can isn't used

        _lastSillyStringTarget[player.Guid] = target.Guid;

        var recipients = new HashSet<Player> { player };
        foreach (var visiblePlayer in player.VisiblePlayers.Values)
            recipients.Add(visiblePlayer);

        var sync = new PlayerUpdatePacketSetSynchronizedAnimations();
        sync.Animations.Add(new PlayerUpdatePacketSetSynchronizedAnimations.Animation { Guid = player.Guid, AnimationId = favor.AnimationId });

        foreach (var recipient in recipients)
            recipient.SendTunneled(sync);

        var tagId = System.Threading.Interlocked.Increment(ref _castFxTagCounter);

        var beam = new PlayerUpdatePacketAddEffectTagCompositeEffect
        {
            Guid = target.Guid,
            TagId = tagId,
            CompositeEffectId = favor.EffectId,
            SourceGuid = player.Guid,
        };

        foreach (var recipient in recipients)
            recipient.SendTunneled(beam);

        _logger.LogTrace("Silly String: {who} sprayed {target}.", player.Name, target.Name);

        // Ticked from Player.UpdateEveryTick's delayed-packet queue - no background tasks/threads.
        player.SendTunneledToVisibleDelayed(new PlayerUpdatePacketSetAnimation
        {
            Guid = player.Guid,
            AnimationId = BoomboxIdleAnimId,
            Flags = 1
        }, (int)(favor.GestureSeconds * 1000), sendToSelf: true);

        player.SendTunneledToVisibleDelayed(
            new PlayerUpdatePacketRemoveEffectTagCompositeEffect { Guid = target.Guid, TagId = tagId },
            (int)(favor.EffectSeconds * 1000), sendToSelf: true);

        StartCooldown(player.Guid, itemDefinition.Id, favor.CooldownMs);

        var count = clientItem.Count;
        var hasItemLeft = !itemDefinition.SingleUse || count > 1;

        if (itemDefinition.SingleUse)
            ConsumeItem(connection, clientItem, itemDefinition, slot);

        if (hasItemLeft)
            player.StartActionBarCooldown(2, slot, itemDefinition.Icon.Id, itemDefinition.NameId,
                itemDefinition.SingleUse ? count - 1 : count, favor.CooldownMs, IconTintId(clientItem, itemDefinition));

        return true;
    }
}
