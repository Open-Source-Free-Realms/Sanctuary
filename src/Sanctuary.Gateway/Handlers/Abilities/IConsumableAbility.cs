using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Handlers.Abilities;

// A per-category item-ability handler (boombox, silly string, etc.), tried in order by
// AbilityPacketClientRequestStartAbilityHandler.HandleItemAbility until one matches.
public interface IConsumableAbility
{
    bool Matches(ClientItemDefinition itemDefinition);

    bool Handle(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition);
}
