using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Catch-all for items no other ability claims. Registered last. These items have no cooldown
// to show a radial for, so it just plays their effect and eats them.
public sealed class DefaultConsumableAbility(AbilityServices services) : ConsumableAbility(services)
{
    public override bool Matches(ClientItemDefinition itemDefinition) => true;

    public override bool HandleAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        PlayEffect(connection, itemDefinition.CompositeEffectId);

        if (itemDefinition.SingleUse)
            return ConsumeItem(connection, clientItem, itemDefinition, slot);

        return true;
    }
}
