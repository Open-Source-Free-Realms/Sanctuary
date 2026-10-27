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
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketInGamePurchasePlaceOrderPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IHousingManager _housingManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketBaseInGamePurchaseHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _housingManager = serviceProvider.GetRequiredService<IHousingManager>();
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

        var packetInGamePurchasePlaceOrderResponse = new PacketInGamePurchasePlaceOrderResponse
        {
            OrderTrackingId = packet.Order.OrderTrackingId
        };

        if (!PacketInGamePurchasePreviewOrderHandler.TryPriceOrder(connection, packet.Order, out var orderLines, out var totalCost))
            return SendResult(connection, packetInGamePurchasePlaceOrderResponse, 2);

        if (connection.Player.StationCash < totalCost)
            return SendResult(connection, packetInGamePurchasePlaceOrderResponse, 5);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters
            .Include(x => x.Items)
            .Include(x => x.Mounts)
            .Include(x => x.Houses)
            .AsSplitQuery()
            .SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
            return SendResult(connection, packetInGamePurchasePlaceOrderResponse, 2);

        var lastItemId = dbCharacter.Items.Count > 0 ? dbCharacter.Items.Max(i => i.Id) : 0;
        var lastMountId = dbCharacter.Mounts.Count > 0 ? dbCharacter.Mounts.Max(x => x.Id) : 0;

        var pendingUpdates = new List<Action>();
        var purchasedHouses = new List<HouseDefinition>();

        foreach (var (orderDetail, appStoreBundleDefinition, orderDetailTint) in orderLines)
        {
            foreach (var bundleEntry in appStoreBundleDefinition.Entries)
            {
                if (!_resourceManager.ClientItemDefinitions.TryGetValue(bundleEntry.MarketingItemId, out var clientItemDefinition))
                    return SendResult(connection, packetInGamePurchasePlaceOrderResponse, 2);

                if (clientItemDefinition.Type == 19) // Mounts
                {
                    if (!_resourceManager.Mounts.TryGetValue(clientItemDefinition.Param1, out var mountDefinition))
                        return SendResult(connection, packetInGamePurchasePlaceOrderResponse, 2);

                    if (dbCharacter.Mounts.Any(x => x.Definition == mountDefinition.Id && x.Tint == orderDetailTint))
                        return SendResult(connection, packetInGamePurchasePlaceOrderResponse, 2);

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
                        if (!connection.Player.Mounts.Any(x => x.Definition == mountDef.Id && x.TintId == orderDetailTint))
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
                        }

                        var packetMountList = new PacketMountList
                        {
                            Mounts = connection.Player.Mounts
                        };

                        connection.Player.SendTunneled(packetMountList);
                    });

                    continue;
                }

                if (clientItemDefinition.Type == 16) // Houses
                {
                    var houseDefinition = _resourceManager.Houses.Values
                        .FirstOrDefault(x => x.ItemDefinitionIds.Contains(clientItemDefinition.Id));

                    // Never charge for a house that has no zone or is already owned.
                    if (houseDefinition is null
                        || dbCharacter.Houses.Any(x => x.Definition == houseDefinition.Id)
                        || purchasedHouses.Contains(houseDefinition))
                        return SendResult(connection, packetInGamePurchasePlaceOrderResponse, 2);

                    purchasedHouses.Add(houseDefinition);
                }

                // Everything else is an inventory item (clothing, pets, furniture, recipes, house items, ...).
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
        }

        dbCharacter.StationCash -= totalCost;

        if (dbContext.SaveChanges() <= 0)
            return SendResult(connection, packetInGamePurchasePlaceOrderResponse, 2);

        connection.Player.StationCash = dbCharacter.StationCash;

        foreach (var pendingUpdate in pendingUpdates)
            pendingUpdate();

        foreach (var houseDefinition in purchasedHouses)
        {
            if (!_housingManager.TryGrantHouse(connection.Player, houseDefinition, out _))
                _logger.LogError("Failed to grant purchased house {house} to {player}.", houseDefinition.Id, connection.Player.Guid);
        }

        packetInGamePurchasePlaceOrderResponse.Result = 1;

        packetInGamePurchasePlaceOrderResponse.OrderId = packet.Order.OrderTrackingId.ToString();

        packetInGamePurchasePlaceOrderResponse.Total = totalCost;

        connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

        PacketInGamePurchaseWalletInfoRequestHandler.SendWalletInfo(connection);

        return true;
    }

    private static bool SendResult(GatewayConnection connection, PacketInGamePurchasePlaceOrderResponse response, int result)
    {
        response.Result = result;

        connection.SendTunneled(response);

        return true;
    }
}
