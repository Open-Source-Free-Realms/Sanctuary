﻿using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Core.IO;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketItemDefinitionRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketItemDefinitionRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PlayerUpdatePacketItemDefinitionRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PlayerUpdatePacketItemDefinitionRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketItemDefinitionRequest), packet);

        if (!_resourceManager.ItemDefinitions.TryGetValue(packet.Id, out var itemDefinition))
        {
            _logger.LogWarning("Received request for unknown item definition. Id: {id}", packet.Id);
            return true;
        }

        using var writer = new PacketWriter();

        var clientItemDefinition = new ClientItemDefinition(itemDefinition);

        clientItemDefinition.Serialize(writer);

        packet.Payload = writer.Buffer;

        connection.SendTunneled(packet);

        return true;
    }
}