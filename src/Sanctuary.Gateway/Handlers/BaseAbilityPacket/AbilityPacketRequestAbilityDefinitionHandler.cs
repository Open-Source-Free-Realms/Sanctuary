using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Gateway.Combat;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AbilityPacketRequestAbilityDefinitionHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(AbilityPacketRequestAbilityDefinitionHandler));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!AbilityPacketRequestAbilityDefinition.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(AbilityPacketRequestAbilityDefinition));
            return false;
        }

        _logger.LogInformation(
            "Received ability definition request. ( AbilityDefinitionId: {abilityDefinitionId}, ActiveProfileId: {activeProfileId} )",
            packet.AbilityDefinitionId,
            connection.Player.ActiveProfileId);

        if (packet.AbilityDefinitionId == 0)
        {
            CombatBootstrap.SendForActiveProfile(connection, _resourceManager, _logger);
            return true;
        }

        return CombatBootstrap.TrySendAbilityDefinition(connection, packet.AbilityDefinitionId, _logger);
    }
}

