using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Food with just a visual/chat effect. Only ability that touches FoodEffects, so the lookup
// lives here rather than being repeated by the catch-all.
public sealed class FoodEffectAbility(AbilityServices services) : ConsumableAbility(services)
{
    private const int FoodEffectCooldownMs = 120_000;

    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.FoodEffects.ContainsKey(itemDefinition.ActivatableAbilityId);

    public override bool HandleAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        if (connection.Player.IsItemOnCooldown(itemDefinition.Id))
            return SendFailure(connection);

        connection.Player.StartItemCooldown(itemDefinition.Id, FoodEffectCooldownMs);

        _resourceManager.Consumables.FoodEffects.TryGetValue(itemDefinition.ActivatableAbilityId, out var foodEffect);

        if (foodEffect?.QuickChatId is int quickChatId and not 0)
        {
            connection.Player.SendTunneledToVisible(new QuickChatSendChatToChannelPacket
            {
                Id = quickChatId,
                Guid = connection.Player.Guid,
                Name = connection.Player.Name ?? new NameData(),
                Channel = ChatChannel.WorldArea,
                AreaNameId = 0,
                GuildGuid = 0
            }, true);
        }

        PlayEffect(connection, foodEffect?.CompositeEffectId ?? itemDefinition.CompositeEffectId, foodEffect?.EffectDelayMs ?? 0);

        FinishActivation(connection, clientItem, itemDefinition, slot, FoodEffectCooldownMs);

        return true;
    }
}
