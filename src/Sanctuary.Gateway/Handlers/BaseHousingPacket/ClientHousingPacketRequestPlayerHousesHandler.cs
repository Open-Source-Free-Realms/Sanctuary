using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
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
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketRequestPlayerHousesHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketRequestPlayerHouses.TryDeserialize(data, out var clientHousingPacketRequestPlayerHouses))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketRequestPlayerHouses));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketRequestPlayerHouses), clientHousingPacketRequestPlayerHouses);

        var houseOwner = connection.Player;

        if (clientHousingPacketRequestPlayerHouses.PlayerGuid != houseOwner.Guid && !_zoneManager.TryGetPlayer(clientHousingPacketRequestPlayerHouses.PlayerGuid, out houseOwner))
        {
            _logger.LogWarning("{player} requested the houses of {owner}, who is not online.", connection.Player.Guid, clientHousingPacketRequestPlayerHouses.PlayerGuid);
            return true;
        }

        var housingPacketInstanceList = HousingHelper.GetInstanceList(_resourceManager, _dbContextFactory, houseOwner);

        connection.SendTunneled(housingPacketInstanceList);

        return true;
    }
}
