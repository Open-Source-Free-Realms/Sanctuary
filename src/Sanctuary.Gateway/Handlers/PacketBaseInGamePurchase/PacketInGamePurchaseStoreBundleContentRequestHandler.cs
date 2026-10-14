using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketInGamePurchaseStoreBundleContentRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketBaseInGamePurchaseHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketInGamePurchaseStoreBundleContentRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketInGamePurchaseStoreBundleContentRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketInGamePurchaseStoreBundleContentRequest), packet);

        var packetInGamePurchaseStoreBundleContentResponse = new PacketInGamePurchaseStoreBundleContentResponse();

        foreach (var request in packet.Requests)
        {
            packetInGamePurchaseStoreBundleContentResponse.Responses.Add(new PacketInGamePurchaseStoreBundleContentResponse.ResponseData
            {
                StoreId = request.StoreId,
                BundleId = request.BundleId,
            });
        }

        connection.SendTunneled(packetInGamePurchaseStoreBundleContentResponse);

        List<ClientItemDefinition> clientItemDefinitions = [];

        foreach (var request in packet.Requests)
        {
            foreach (var marketingItemId in request.MarketingItemIds)
            {
                if (!_resourceManager.ClientItemDefinitions.TryGetValue(marketingItemId, out var clientItemDefinition))
                {
                    _logger.LogWarning("Received request for unknown item definition. Id: {id}", marketingItemId);
                    continue;
                }

                clientItemDefinitions.Add(ItemDefinitionHelper.GetPlayerItemDefinition(connection.Player, _resourceManager, clientItemDefinition));
            }
        }

        if (clientItemDefinitions.Count == 0)
            return true;

        using var writer = new PacketWriter();

        writer.Write(clientItemDefinitions);

        var playerUpdatePacketItemDefinitions = new PlayerUpdatePacketItemDefinitions();

        playerUpdatePacketItemDefinitions.Payload = writer.Buffer;

        connection.SendTunneled(playerUpdatePacketItemDefinitions);

        return true;
    }
}