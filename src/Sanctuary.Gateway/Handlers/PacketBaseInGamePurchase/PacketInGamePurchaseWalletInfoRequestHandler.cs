using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketInGamePurchaseWalletInfoRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketBaseInGamePurchaseHandler));
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(PacketInGamePurchaseWalletInfoRequest));

        SendWalletInfo(connection);

        return true;
    }

    /// <summary>
    /// Also used to refresh the marketplace's Station Cash balance after a purchase.
    /// </summary>
    public static void SendWalletInfo(GatewayConnection connection)
    {
        var packetInGamePurchaseWalletInfoResponse = new PacketInGamePurchaseWalletInfoResponse
        {
            ErrorCode = 1,
            WalletInfo =
            {
                Unknown = true,
                StationCash = connection.Player.StationCash,
                CreditCardId = 69420,
                CurrencyCode = "SOE",
                International = true
            }
        };

        connection.SendTunneled(packetInGamePurchaseWalletInfoResponse);
    }
}