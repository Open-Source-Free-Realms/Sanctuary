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
public static class ClientHousingPacketPreviewByItemDefinitionIdRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketPreviewByItemDefinitionIdRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketPreviewByItemDefinitionIdRequest.TryDeserialize(data, out var clientHousingPacketPreviewByItemDefinitionIdRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketPreviewByItemDefinitionIdRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketPreviewByItemDefinitionIdRequest), clientHousingPacketPreviewByItemDefinitionIdRequest);

        var player = connection.Player;

        if (!HousingHelper.TryPreviewHouse(_resourceManager, _dbContextFactory, player, clientHousingPacketPreviewByItemDefinitionIdRequest.ItemDefinitionId, out var housingError))
        {
            _logger.LogWarning("{player} could not preview house item {itemDefinitionId}. ( {error} )", player.Guid, clientHousingPacketPreviewByItemDefinitionIdRequest.ItemDefinitionId, housingError);

            connection.SendTunneled(new HousingPacketHousingError { Error = housingError });
        }

        return true;
    }
}