using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketRequestVisitToNeighborHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketRequestVisitToNeighborHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketRequestVisitToNeighbor.TryDeserialize(data, out var clientHousingPacketRequestVisitToNeighbor))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketRequestVisitToNeighbor));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketRequestVisitToNeighbor), clientHousingPacketRequestVisitToNeighbor);

        var player = connection.Player;

        _logger.LogInformation("{player} requested a visit to neighbor {guid}.", player.Guid, clientHousingPacketRequestVisitToNeighbor.Guid);

        if (!HousingHelper.TryEnterHouse(_resourceManager, _dbContextFactory, _zoneManager, player, clientHousingPacketRequestVisitToNeighbor.Guid, out var housingError))
        {
            _logger.LogWarning("{player} could not visit {guid}. ( {error} )", player.Guid, clientHousingPacketRequestVisitToNeighbor.Guid, housingError);

            connection.SendTunneled(new HousingPacketHousingError { Error = housingError });
        }

        return true;
    }
}