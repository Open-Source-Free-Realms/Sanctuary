using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketRequestPlayerHousesHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketRequestPlayerHousesHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketRequestPlayerHouses.TryDeserialize(data, out var clientHousingPacketRequestPlayerHouses))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketRequestPlayerHouses));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketRequestPlayerHouses), clientHousingPacketRequestPlayerHouses);

        var housingPacketInstanceList = HousingHelper.GetInstanceList(_resourceManager, connection.Player);

        connection.SendTunneled(housingPacketInstanceList);

        return true;
    }
}
