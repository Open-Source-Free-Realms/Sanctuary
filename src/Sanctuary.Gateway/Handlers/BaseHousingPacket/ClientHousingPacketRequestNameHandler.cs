using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketRequestNameHandler
{
    private static ILogger _logger = null!;

    private const int MaxHouseNameLength = 32;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketRequestNameHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketRequestName.TryDeserialize(data, out var clientHousingPacketRequestName))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketRequestName));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketRequestName), clientHousingPacketRequestName);

        if (connection.Player.Zone is not HousingZone housingZone || !housingZone.IsOwner(connection.Player))
            return true;

        var houseName = clientHousingPacketRequestName.Name.Trim();

        clientHousingPacketRequestName.IsAccepted = houseName.Length > 0 && houseName.Length <= MaxHouseNameLength;

        if (clientHousingPacketRequestName.IsAccepted)
        {
            clientHousingPacketRequestName.Name = houseName;
            housingZone.SetHouseName(houseName);
        }

        connection.SendTunneled(clientHousingPacketRequestName);

        return true;
    }
}