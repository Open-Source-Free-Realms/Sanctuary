using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AbilityPacketRequestAbilityDefinitionHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(AbilityPacketRequestAbilityDefinitionHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!AbilityPacketRequestAbilityDefinition.TryDeserialize(data, out var abilityPacketRequestAbilityDefinition))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(AbilityPacketRequestAbilityDefinition));
            return false;
        }

        Sanctuary.Game.Interactions.SnowballInteraction.Update(connection.Player);
        var definition = connection.Player.NpcAbility?.Definition;
        if (definition is not null && definition.Id == abilityPacketRequestAbilityDefinition.AbilityId)
            connection.SendTunneled(new AbilityPacketAbilityDefinition { Definition = definition });
        return true;
    }
}
