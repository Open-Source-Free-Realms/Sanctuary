using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game;
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

        // The coin store asks for the definitions it's missing; without them its cards stay blank.
        var clientItemDefinitions = new List<ClientItemDefinition>();

        foreach (var itemDefinitionId in packet.ItemDefinitions)
        {
            if (_resourceManager.CoinStoreItems.ContainsKey(itemDefinitionId)
                && _resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var clientItemDefinition))
            {
                connection.Player.SentCoinStoreItemDefinitionIds.TryAdd(itemDefinitionId);
                clientItemDefinitions.Add(clientItemDefinition);
            }
        }

        if (clientItemDefinitions.Count > 0)
        {
            using var writer = new PacketWriter();

            writer.Write(clientItemDefinitions);

            connection.SendTunneled(new PlayerUpdatePacketItemDefinitions
            {
                Payload = writer.Buffer
            });
        }

        var coinStoreItemDefinitionsResponsePacket = new CoinStoreItemDefinitionsResponsePacket();

        coinStoreItemDefinitionsResponsePacket.Success = true;

        coinStoreItemDefinitionsResponsePacket.ItemDefinitions = packet.ItemDefinitions;

        connection.SendTunneled(coinStoreItemDefinitionsResponsePacket);

        return true;
    }
}
