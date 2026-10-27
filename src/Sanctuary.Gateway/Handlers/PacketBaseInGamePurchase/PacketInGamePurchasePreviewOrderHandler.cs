using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Packet.Common.GameCommerce;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketInGamePurchasePreviewOrderHandler
{
    private const string FreeCouponCode = "OSFR";

    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketBaseInGamePurchaseHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketInGamePurchasePreviewOrder.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketInGamePurchasePreviewOrder));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketInGamePurchasePreviewOrder), packet);

        var packetInGamePurchasePreviewOrderResponse = new PacketInGamePurchasePreviewOrderResponse
        {
            OrderTrackingId = packet.Order.OrderTrackingId,
            Result = 8
        };

        if (packet.Order.CouponCode == FreeCouponCode)
        {
            packetInGamePurchasePreviewOrderResponse.Result = 1;
            packetInGamePurchasePreviewOrderResponse.Discount = 0;
            packetInGamePurchasePreviewOrderResponse.Total = 0;
        }
        else if (string.IsNullOrEmpty(packet.Order.CouponCode))
        {
            if (TryPriceOrder(connection, packet.Order, out _, out var totalCost))
            {
                packetInGamePurchasePreviewOrderResponse.Result = connection.Player.StationCash < totalCost ? 5 : 1;
                packetInGamePurchasePreviewOrderResponse.Total = totalCost;
            }
            else
            {
                packetInGamePurchasePreviewOrderResponse.Result = 2;
            }
        }

        connection.SendTunneled(packetInGamePurchasePreviewOrderResponse);

        return true;
    }

    /// <summary>
    /// Resolves every line of the order to its store bundle and prices the whole cart.
    /// </summary>
    public static bool TryPriceOrder(GatewayConnection connection, ClientInGamePurchaseOrder order,
        out List<(BaseInGamePurchaseOrderDetail Detail, StoreBundleDefinition Bundle, int Tint)> orderLines, out int totalCost)
    {
        orderLines = [];
        totalCost = 0;

        if (order.Details.Count == 0)
            return false;

        long totalCostValue = 0;

        foreach (var orderDetail in order.Details)
        {
            if (orderDetail.Quantity <= 0
                || !_resourceManager.Stores.TryGetValue(orderDetail.StoreId, out var storeDefinition)
                || !storeDefinition.Bundles.TryGetValue(orderDetail.StoreBundleId, out var appStoreBundleDefinition)
                || !int.TryParse(orderDetail.Tint, out var orderDetailTint))
                return false;

            var cost = connection.Player.MembershipStatus == 0
                ? appStoreBundleDefinition.Price
                : appStoreBundleDefinition.MemberDiscount;

            totalCostValue += (long)cost * orderDetail.Quantity;

            if (totalCostValue < 0 || totalCostValue > int.MaxValue)
                return false;

            orderLines.Add((orderDetail, appStoreBundleDefinition, orderDetailTint));
        }

        totalCost = (int)totalCostValue;

        return true;
    }
}
