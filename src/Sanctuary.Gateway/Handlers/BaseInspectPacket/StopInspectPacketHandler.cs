using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class StopInspectPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(StopInspectPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!StopInspectPacket.TryDeserialize(data, out var stopInspectPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(StopInspectPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(StopInspectPacket), stopInspectPacket);

        foreach (var itemDefinitionId in connection.Player.InspectedItemDefinitionIds)
        {
            if (_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var clientItemDefinition))
                ItemDefinitionHelper.SendItemDefinition(connection.Player, _resourceManager, clientItemDefinition);
        }

        connection.Player.InspectedItemDefinitionIds.Clear();

        return true;
    }
}
