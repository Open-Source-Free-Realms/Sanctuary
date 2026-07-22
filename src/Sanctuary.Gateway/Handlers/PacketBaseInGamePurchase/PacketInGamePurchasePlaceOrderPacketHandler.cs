using System;
using System.Collections.Generic;
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

        var dbCharacter = dbContext.Characters
            .Include(x => x.Items)
            .Include(x => x.Mounts)
            .AsSplitQuery()
            .SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
        {
            packetInGamePurchasePlaceOrderResponse.Result = 2;

            connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

            return true;
        }

        var lastItemId = dbCharacter.Items.Count > 0 ? dbCharacter.Items.Max(i => i.Id) : 0;
        var lastMountId = dbCharacter.Mounts.Count > 0 ? dbCharacter.Mounts.Max(x => x.Id) : 0;

        var pendingUpdates = new List<Action>();

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

                var dbItem = dbCharacter.Items.SingleOrDefault(i =>
                    i.Definition == clientItemDefinition.Id && i.Tint == orderDetailTint);

                if (dbItem is not null)
                {
                    dbItem.Count += totalQuantity;
                }
                else
                {
                    dbItem = new DbItem
                    {
                        Id = ++lastItemId,
                        Definition = clientItemDefinition.Id,
                        Tint = orderDetailTint,

                        Count = totalQuantity
                    };

                    dbCharacter.Items.Add(dbItem);
                }

                var itemDefinition = clientItemDefinition;
                var savedItem = dbItem;

                pendingUpdates.Add(() =>
                {
                    var clientItem = connection.Player.Items.SingleOrDefault(x => x.Definition == itemDefinition.Id && x.Tint == orderDetailTint);

                    var addItem = false;

                    if (clientItem is not null)
                    {
                        clientItem.Count = savedItem.Count;
                    }
                    else
                    {
                        addItem = true;

                        clientItem = new ClientItem
                        {
                            Id = savedItem.Id,
                            Tint = savedItem.Tint,
                            Count = savedItem.Count,
                            Definition = savedItem.Definition
                        };

                        connection.Player.Items.Add(clientItem);
                    }

                    if (addItem)
                    {
                        using var writer = new PacketWriter();

                        clientItem.Serialize(writer);

                        itemDefinition.Serialize(writer);

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
                });
            }
            else if (clientItemDefinition.Type == 19) // Mounts
            {
                if (!_resourceManager.Mounts.TryGetValue(clientItemDefinition.Param1, out var mountDefinition))
                {
                    packetInGamePurchasePlaceOrderResponse.Result = 2;

                    connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

                    return true;
                }

                if (dbCharacter.Mounts.Any(x => x.Definition == mountDefinition.Id && x.Tint == orderDetailTint))
                {
                    packetInGamePurchasePlaceOrderResponse.Result = 2;

                    connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

                    return true;
                }

                var dbMount = new DbMount
                {
                    Id = ++lastMountId,

                    Tint = orderDetailTint,
                    Definition = mountDefinition.Id,
                    IsUpgraded = mountDefinition.IsUpgradable // TODO: Implement training
                };

                dbCharacter.Mounts.Add(dbMount);

                var mountDef = mountDefinition;
                var savedMount = dbMount;

                pendingUpdates.Add(() =>
                {
                    connection.Player.Mounts.Add(new PacketMountInfo
                    {
                        Id = savedMount.Id,
                        Definition = mountDef.Id,
                        NameId = mountDef.NameId,
                        ImageSetId = mountDef.ImageSetId,
                        TintId = orderDetailTint,
                        MembersOnly = mountDef.MembersOnly,
                        IsUpgradable = mountDef.IsUpgradable,
                        IsUpgraded = savedMount.IsUpgraded
                    });

                    var packetMountList = new PacketMountList
                    {
                        Mounts = connection.Player.Mounts
                    };

                    connection.Player.SendTunneled(packetMountList);
                });
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

        foreach (var pendingUpdate in pendingUpdates)
            pendingUpdate();

        packetInGamePurchasePlaceOrderResponse.Result = 1;

        packetInGamePurchasePlaceOrderResponse.OrderTrackingId = packet.Order.OrderTrackingId;
        packetInGamePurchasePlaceOrderResponse.OrderId = packet.Order.OrderTrackingId.ToString();

        packetInGamePurchasePlaceOrderResponse.Total = totalCost;

        connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

        return true;
    }
}