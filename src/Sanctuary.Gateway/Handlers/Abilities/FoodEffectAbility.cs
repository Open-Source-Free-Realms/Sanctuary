using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Food with just a plain visual/chat effect, nothing fancier - owns the FoodEffects lookup itself,
// since it's the only ability that ever matches one. (The handler's generic fallback for anything
// unrecognized never gets a FoodEffects hit: if an item had one, IsInCollection below would already
// have claimed it before dispatch reaches the fallback.)
public sealed class FoodEffectAbility : ConsumableAbility
{
    private const int FoodEffectCooldownMs = 120_000;

    public override bool IsInCollection(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.FoodEffects.ContainsKey(itemDefinition.ActivatableAbilityId);

    public override bool HandleAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        if (IsOnCooldown(connection.Player.Guid, itemDefinition.Id))
            return SendFailure(connection);

        StartCooldown(connection.Player.Guid, itemDefinition.Id, FoodEffectCooldownMs);

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
