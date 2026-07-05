using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Gateway.Services;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class WallOfDataUIEventPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(WallOfDataUIEventPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!WallOfDataUIEventPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(WallOfDataUIEventPacket));
            return false;
        }

        _logger.LogInformation(
    "{connection} received WallOfData UI event. ( TableName: {tableName}, Callback: {callback}, Param: {param} )",
    connection,
    packet.TableName,
    packet.Callback,
    packet.Param);

        if (IsMarketplaceOpenEvent(packet))
        {
            ItemActionBarService.ReplayOwnedCarouselItemsForMarketplaceOpen(connection, _resourceManager, _logger);

            _logger.LogInformation(
                "{connection} replayed owned quick-items after marketplace UI opened for carousel population.",
                connection);
        }

        return true;
    }

    private static bool IsMarketplaceOpenEvent(WallOfDataUIEventPacket packet)
    {
        return string.Equals(packet.TableName, "Marketplace", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(packet.TableName, "GameDock", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(packet.Callback, "Open Marketplace", StringComparison.OrdinalIgnoreCase));
    }
}
