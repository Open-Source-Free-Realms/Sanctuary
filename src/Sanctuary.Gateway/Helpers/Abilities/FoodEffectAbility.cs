using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Helpers.Abilities;

// Food with just a visual/chat effect. Only ability that touches FoodEffects, so the lookup
// lives here rather than being repeated by the catch-all.
public sealed class FoodEffectAbility(AbilityServices services) : ConsumableAbility(services)
{
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

        ApplyFoodEffect(player, itemDefinition.NameId, foodEffect?.CompositeEffectId ?? itemDefinition.CompositeEffectId, foodEffect?.EffectDelayMs ?? 0);

        FinishActivation(player, clientItem, itemDefinition, slot, FoodEffectCooldownMs);

        return true;
    }
}
