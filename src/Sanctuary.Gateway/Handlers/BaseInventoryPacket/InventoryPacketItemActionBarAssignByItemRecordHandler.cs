using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Gateway.Services;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketItemActionBarAssignByItemRecordHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketItemActionBarAssignByItemRecordHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketItemActionBarAssignByItemRecord.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketItemActionBarAssignByItemRecord));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( Slot: {slot}, Definition: {definition}, Tint: {tint} )",
            nameof(InventoryPacketItemActionBarAssignByItemRecord),
            packet.Slot,
            packet.Item.Definition,
            packet.Item.Tint);

        return ItemActionBarService.TryAssignItemByRecord(
            connection,
            _resourceManager,
            _dbContextFactory,
            packet.Slot,
            packet.Item,
            _logger);
    }
}
