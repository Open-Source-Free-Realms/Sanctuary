using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

// Catch-all for items no other ability claims. Registered last. These items have no cooldown
// to show a radial for, so it just plays their effect and eats them.
public sealed class DefaultConsumableAbility(AbilityServices services) : ConsumableAbility(services)
{
    public override bool Matches(ClientItemDefinition itemDefinition) => true;

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        PlayEffect(player, itemDefinition.CompositeEffectId);

        if (itemDefinition.SingleUse)
            return ConsumeItem(player, clientItem, itemDefinition, slot);

        return true;
    }
}
