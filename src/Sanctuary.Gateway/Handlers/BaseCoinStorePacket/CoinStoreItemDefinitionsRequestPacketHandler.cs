using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CoinStoreItemDefinitionsRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CoinStoreItemDefinitionsRequestPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CoinStoreItemDefinitionsRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CoinStoreItemDefinitionsRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CoinStoreItemDefinitionsRequestPacket), packet);

        List<ClientItemDefinition> clientItemDefinitions = [];

        foreach (var itemDefinitionId in packet.ItemDefinitions)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var clientItemDefinition))
            {
                _logger.LogWarning("Received request for unknown item definition. Id: {id}", itemDefinitionId);
                continue;
            }

            connection.Player.SentCoinStoreItemDefinitionIds.TryAdd(itemDefinitionId);
            clientItemDefinitions.Add(clientItemDefinition);
        }

        ItemDefinitionHelper.SendItemDefinitions(connection.Player, _resourceManager, clientItemDefinitions);

        var coinStoreItemDefinitionsResponsePacket = new CoinStoreItemDefinitionsResponsePacket();

        coinStoreItemDefinitionsResponsePacket.Success = true;

        coinStoreItemDefinitionsResponsePacket.ItemDefinitions = packet.ItemDefinitions;

        connection.SendTunneled(coinStoreItemDefinitionsResponsePacket);

        return true;
    }
}