using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Helpers.Abilities;

// Food with just a visual/chat effect. Only ability that touches FoodEffects, so the lookup
// lives here rather than being repeated by the catch-all.
public sealed class FoodEffectAbility(AbilityServices services) : ConsumableAbility(services)
{
    private const int FoodEffectDurationMs = 30 * 60 * 1000;
    private const int FoodEffectCooldownMs = 30 * 60 * 1000;

    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.FoodEffects.ContainsKey(itemDefinition.ActivatableAbilityId);

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        if (player.IsItemOnCooldown(itemDefinition.Id))
            return SendFailure(player);

        player.StartItemCooldown(itemDefinition.Id, FoodEffectCooldownMs);

        _resourceManager.Consumables.FoodEffects.TryGetValue(itemDefinition.ActivatableAbilityId, out var foodEffect);

        if (foodEffect?.QuickChatId is int quickChatId and not 0)
        {
            player.SendTunneledToVisible(new QuickChatSendChatToChannelPacket
            {
                Id = quickChatId,
                Guid = player.Guid,
                Name = player.Name ?? new NameData(),
                Channel = ChatChannel.WorldArea,
                AreaNameId = 0,
                GuildGuid = 0
            }, true);
        }

        ApplyFoodAura(player, foodEffect?.CompositeEffectId ?? itemDefinition.CompositeEffectId, foodEffect?.EffectDelayMs ?? 0);

        FinishActivation(player, clientItem, itemDefinition, slot, FoodEffectCooldownMs);

        return true;
    }

    private static void ApplyFoodAura(Player player, int effectId, int delayMs)
    {
        if (effectId == 0)
            return;

        if (player.ActiveFoodEffectTagId != 0)
        {
            player.SendTunneledToVisible(new PlayerUpdatePacketRemoveEffectTagCompositeEffect
            {
                Guid = player.Guid,
                TagId = player.ActiveFoodEffectTagId
            }, true);
        }

        var tagId = NextEffectTagId();
        player.ActiveFoodEffectTagId = tagId;

        var addAura = new PlayerUpdatePacketAddEffectTagCompositeEffect
        {
            Guid = player.Guid,
            TagId = tagId,
            CompositeEffectId = effectId,
            SourceGuid = player.Guid
        };

        if (delayMs > 0)
            player.SendTunneledToVisibleDelayed(addAura, delayMs, true);
        else
            player.SendTunneledToVisible(addAura, true);

        player.SendTunneledToVisibleDelayed(new PlayerUpdatePacketRemoveEffectTagCompositeEffect
        {
            Guid = player.Guid,
            TagId = tagId
        }, delayMs + FoodEffectDurationMs, true);
    }
}
