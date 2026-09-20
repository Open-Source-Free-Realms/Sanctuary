using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

public sealed class SillyStringAbility(AbilityServices services) : ConsumableAbility(services)
{
    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.PartyFavors.ContainsKey(itemDefinition.Id);

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        _resourceManager.Consumables.PartyFavors.TryGetValue(itemDefinition.Id, out var favor);

        if (player.IsItemOnCooldown(itemDefinition.Id))
            return SendFailure(player);

        // Not aimable, so there's no selected target to honour. Skip last time's victim unless
        // they're the only one around.
        var target = player.FindNearestPlayer(favor!.Range, player.LastSillyStringTarget)
            ?? player.FindNearestPlayer(favor.Range);

        if (target is null)
            return SendFailure(player); // nobody nearby to spray - can isn't used

        player.LastSillyStringTarget = target.Guid;

        var recipients = new HashSet<Player> { player };
        foreach (var visiblePlayer in player.VisiblePlayers.Values)
            recipients.Add(visiblePlayer);

        var sync = new PlayerUpdatePacketSetSynchronizedAnimations();
        sync.Animations.Add(new PlayerUpdatePacketSetSynchronizedAnimations.Animation { Guid = player.Guid, AnimationId = favor.AnimationId });

        foreach (var recipient in recipients)
            recipient.SendTunneled(sync);

        var tagId = NextEffectTagId();

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

        // Ticked from Player.UpdateEveryTick's delayed-packet queue, no background tasks.
        player.SendTunneledToVisibleDelayed(new PlayerUpdatePacketSetAnimation
        {
            Guid = player.Guid,
            AnimationId = IdleAnimationId,
            Flags = 1
        }, (int)(favor.GestureSeconds * 1000), sendToSelf: true);

        player.SendTunneledToVisibleDelayed(
            new PlayerUpdatePacketRemoveEffectTagCompositeEffect { Guid = target.Guid, TagId = tagId },
            (int)(favor.EffectSeconds * 1000), sendToSelf: true);

        player.StartItemCooldown(itemDefinition.Id, ClampCooldown(favor.CooldownMs));

        FinishActivation(player, clientItem, itemDefinition, slot, ClampCooldown(favor.CooldownMs), IconTintId(clientItem, itemDefinition.Icon.TintId));

        return true;
    }
}
