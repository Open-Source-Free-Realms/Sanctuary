using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketInGamePurchasePlaceOrderPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketBaseInGamePurchaseHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketInGamePurchasePlaceOrderPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketInGamePurchasePlaceOrderPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketInGamePurchasePlaceOrderPacket), packet);

        var packetInGamePurchasePlaceOrderResponse = new PacketInGamePurchasePlaceOrderResponse();

        var orderDetail = packet.Order.Details.FirstOrDefault();

        if (orderDetail is null)
        {
            packetInGamePurchasePlaceOrderResponse.Result = 2;

            connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

            return true;
        }

        if (!_resourceManager.Stores.TryGetValue(orderDetail.StoreId, out var storeDefinition))
        {
            packetInGamePurchasePlaceOrderResponse.Result = 2;

            connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

            return true;
        }

        if (!storeDefinition.Bundles.TryGetValue(orderDetail.StoreBundleId, out var appStoreBundleDefinition))
        {
            packetInGamePurchasePlaceOrderResponse.Result = 2;

            connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

            return true;
        }

        if (!int.TryParse(orderDetail.Tint, out var orderDetailTint))
        {
            packetInGamePurchasePlaceOrderResponse.Result = 2;

            connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

            return true;
        }

        var cost = connection.Player.MembershipStatus == 0
            ? appStoreBundleDefinition.Price
            : appStoreBundleDefinition.MemberDiscount;

        var totalCost = cost * orderDetail.Quantity;

        if (connection.Player.StationCash < totalCost)
        {
            packetInGamePurchasePlaceOrderResponse.Result = 5;

            connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
        {
            packetInGamePurchasePlaceOrderResponse.Result = 2;

            connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

            return true;
        }

        var lastItemId = dbContext.Items.Where(i => i.CharacterId == dbCharacter.Id)
            .Select(i => (int?)i.Id)
            .Max() ?? 0;

        foreach (var bundleEntry in appStoreBundleDefinition.Entries)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(bundleEntry.MarketingItemId, out var clientItemDefinition))
            {
                packetInGamePurchasePlaceOrderResponse.Result = 2;

                connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

                return true;
            }

            if (clientItemDefinition.Type == 1 || clientItemDefinition.Type == 12)
            {
                var totalQuantity = orderDetail.Quantity * bundleEntry.Quantity;

                var dbItem = dbContext.Items.SingleOrDefault(i => i.CharacterId == dbCharacter.Id &&
                    i.Definition == clientItemDefinition.Id && i.Tint == orderDetailTint);

                if (dbItem is not null)
                {
                    dbItem.Count += totalQuantity;
                }
                else
                {
                    dbItem = new DbItem
                    {
                        Id = lastItemId++ + 1,
                        Definition = clientItemDefinition.Id,
                        Tint = orderDetailTint,

                        Count = totalQuantity
                    };

                    dbCharacter.Items.Add(dbItem);
                }

                var clientItem = connection.Player.Items.SingleOrDefault(x => x.Definition == clientItemDefinition.Id && x.Tint == orderDetailTint);

                var addItem = false;

                if (clientItem is not null)
                {
                    clientItem.Count = dbItem.Count;
                }
                else
                {
                    addItem = true;

                    clientItem = new ClientItem
                    {
                        Id = dbItem.Id,
                        Tint = dbItem.Tint,
                        Count = dbItem.Count,
                        Definition = dbItem.Definition
                    };

                    connection.Player.Items.Add(clientItem);
                }

                if (addItem)
                {
                    using var writer = new PacketWriter();

                    clientItem.Serialize(writer);

                    clientItemDefinition.Serialize(writer);

                    var clientUpdatePacketItemAdd = new ClientUpdatePacketItemAdd();

                    clientUpdatePacketItemAdd.Payload = writer.Buffer;

                    connection.SendTunneled(clientUpdatePacketItemAdd);
                }
                else
                {
                    var clientUpdatePacketItemUpdate = new ClientUpdatePacketItemUpdate
                    {
                        ItemGuid = clientItem.Id,
                        Count = clientItem.Count,
                    };

                    connection.SendTunneled(clientUpdatePacketItemUpdate);
                }
            }
            else if (clientItemDefinition.Type == 19) // Mounts
            {
                if (!_resourceManager.Mounts.TryGetValue(clientItemDefinition.Param1, out var mountDefinition))
                {
                    packetInGamePurchasePlaceOrderResponse.Result = 2;

                    connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

                    return true;
                }

                if (connection.Player.Mounts.Any(x => x.Definition == mountDefinition.Id && x.TintId == orderDetailTint))
                {
                    packetInGamePurchasePlaceOrderResponse.Result = 2;

                    connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

                    return true;
                }

                var lastMountId = dbContext.Mounts.Where(x => x.CharacterId == dbCharacter.Id)
                    .Select(x => (int?)x.Id)
                    .Max() ?? 0;

                var dbMount = new DbMount
                {
                    Id = lastMountId + 1,

                    Tint = orderDetailTint,
                    Definition = mountDefinition.Id,
                    IsUpgraded = mountDefinition.IsUpgradable // TODO: Implement training
                };

                dbCharacter.Mounts.Add(dbMount);

                connection.Player.Mounts.Add(new PacketMountInfo
                {
                    Id = dbMount.Id,
                    Definition = mountDefinition.Id,
                    NameId = mountDefinition.NameId,
                    ImageSetId = mountDefinition.ImageSetId,
                    TintId = orderDetailTint,
                    MembersOnly = mountDefinition.MembersOnly,
                    IsUpgradable = mountDefinition.IsUpgradable,
                    IsUpgraded = dbMount.IsUpgraded
                });

                var packetMountList = new PacketMountList
                {
                    Mounts = connection.Player.Mounts
                };

                connection.Player.SendTunneled(packetMountList);
            }
            else
            {
                // TODO: Implement other item types

                packetInGamePurchasePlaceOrderResponse.Result = 2;

                connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

                return true;
            }
        }

        dbCharacter.StationCash -= totalCost;

        if (dbContext.SaveChanges() <= 0)
        {
            packetInGamePurchasePlaceOrderResponse.Result = 2;

            connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

            return true;
        }

        connection.Player.StationCash = dbCharacter.StationCash;

        packetInGamePurchasePlaceOrderResponse.Result = 1;

        packetInGamePurchasePlaceOrderResponse.OrderTrackingId = packet.Order.OrderTrackingId;
        packetInGamePurchasePlaceOrderResponse.OrderId = packet.Order.OrderTrackingId.ToString();

        packetInGamePurchasePlaceOrderResponse.Total = totalCost;

        connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

        return true;
    }
}