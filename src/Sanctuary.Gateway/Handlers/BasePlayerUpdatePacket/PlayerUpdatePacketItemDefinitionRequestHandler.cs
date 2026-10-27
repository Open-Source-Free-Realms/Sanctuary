using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketItemDefinitionRequestHandler
{
    private const int CoinStoreItemDefinitionBatchSize = 100;

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
        if (!PlayerUpdatePacketItemDefinitionRequest.TryDeserialize(data, out var playerUpdatePacketItemDefinitionRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PlayerUpdatePacketItemDefinitionRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketItemDefinitionRequest), playerUpdatePacketItemDefinitionRequest);

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(playerUpdatePacketItemDefinitionRequest.Id, out var clientItemDefinition))
        {
            _logger.LogWarning("Received request for unknown item definition. Id: {id}", playerUpdatePacketItemDefinitionRequest.Id);
            return true;
        }

        if (_resourceManager.CoinStoreItems.TryGetValue(clientItemDefinition.Id, out var itemDefinitionMetaData))
        {
            SendCoinStoreItemDefinitions(connection.Player, itemDefinitionMetaData);
            return true;
        }

        ItemDefinitionHelper.SendItemDefinition(connection.Player, _resourceManager, clientItemDefinition);

        return true;
    }

    /// <summary>
    /// Sends the requested item, then every coin store definition the player doesn't have yet, in batches.
    /// The coin store asks for one item and shows the rest of the page from what arrives, in its own sort
    /// order; batches that only covered the requested item's neighbours left the other cards blank.
    /// </summary>
    private static void SendCoinStoreItemDefinitions(Player player, ItemDefinitionMetaData requestedItemDefinitionMetaData)
    {
        player.SentCoinStoreItemDefinitionIds.TryAdd(requestedItemDefinitionMetaData.Id);

        var batch = new List<ClientItemDefinition>();

        if (_resourceManager.ClientItemDefinitions.TryGetValue(requestedItemDefinitionMetaData.Id, out var requestedItemDefinition))
            batch.Add(requestedItemDefinition);

        foreach (var coinStoreItem in _resourceManager.CoinStoreItems.Values)
        {
            if (!player.SentCoinStoreItemDefinitionIds.TryAdd(coinStoreItem.Id))
                continue;

            if (!_resourceManager.ClientItemDefinitions.TryGetValue(coinStoreItem.Id, out var clientItemDefinition))
                continue;

            batch.Add(clientItemDefinition);

            if (batch.Count >= CoinStoreItemDefinitionBatchSize)
            {
                SendItemDefinitions(player, batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            SendItemDefinitions(player, batch);
    }

    private static void SendItemDefinitions(Player player, List<ClientItemDefinition> clientItemDefinitions)
    {
        using var writer = new PacketWriter();

        writer.Write(clientItemDefinitions);

        var playerUpdatePacketItemDefinitions = new PlayerUpdatePacketItemDefinitions();

        playerUpdatePacketItemDefinitions.Payload = writer.Buffer;

        player.SendTunneled(playerUpdatePacketItemDefinitions);
    }
}