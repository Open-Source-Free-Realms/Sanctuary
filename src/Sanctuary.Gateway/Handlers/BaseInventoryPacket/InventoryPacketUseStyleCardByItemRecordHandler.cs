using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketUseStyleCardByItemRecordHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketUseStyleCardByItemRecordHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketUseStyleCardByItemRecord.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketUseStyleCardByItemRecord));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketUseStyleCardByItemRecord), packet);

        var clientItem = connection.Player.Items.FirstOrDefault(x => x.Definition == packet.ItemDefinitionId);

        if (clientItem is null)
        {
            _logger.LogWarning("Unknown item definition. {definition}", packet.ItemDefinitionId);
            return true;
        }

        InventoryPacketUseStyleCardHandler.Equip(connection, clientItem);

        return true;
    }
}