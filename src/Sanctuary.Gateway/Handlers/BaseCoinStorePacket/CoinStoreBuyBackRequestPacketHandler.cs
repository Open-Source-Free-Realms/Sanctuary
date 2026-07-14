using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

// Answers the coin-store "Buy Back" request (165/12) with the ack (165/13) the client waits for,
// echoing the EntryId - byte-identical to the real FR server for the captured case. The capture
// shows this reply was a standalone ack (no item/coin update bundled), so we mirror that.
// If in-client testing later shows the sold item should be returned, extend this to repurchase from
// connection.Player.CoinStoreTransactions (the Type==2 sells), mirroring the 165/4 buy path.
// Recovered from live packet captures.
[PacketHandler]
public static class CoinStoreBuyBackRequestPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CoinStoreBuyBackRequestPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CoinStoreBuyBackRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CoinStoreBuyBackRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CoinStoreBuyBackRequestPacket), packet);

        connection.SendTunneled(new CoinStoreBuyBackResponsePacket { EntryId = packet.EntryId });

        return true;
    }
}
