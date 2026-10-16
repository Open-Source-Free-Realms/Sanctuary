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
using Sanctuary.Game.Helpers;
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
        if (!PacketInGamePurchasePlaceOrderPacket.TryDeserialize(data, out var packetInGamePurchasePlaceOrderPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketInGamePurchasePlaceOrderPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketInGamePurchasePlaceOrderPacket), packetInGamePurchasePlaceOrderPacket);

        var packetInGamePurchasePlaceOrderResponse = new PacketInGamePurchasePlaceOrderResponse
        {
            OrderTrackingId = packetInGamePurchasePlaceOrderPacket.Order.OrderTrackingId,
            OrderId = packetInGamePurchasePlaceOrderPacket.Order.OrderTrackingId.ToString()
        };

        var orderDetail = packetInGamePurchasePlaceOrderPacket.Order.Details.FirstOrDefault();

        if (orderDetail is null || orderDetail.Quantity <= 0)
        {
            return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
        }

        if (!_resourceManager.Stores.TryGetValue(orderDetail.StoreId, out var storeDefinition))
        {
            return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
        }

        if (!storeDefinition.Bundles.TryGetValue(orderDetail.StoreBundleId, out var appStoreBundleDefinition))
        {
            return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
        }

        if (!int.TryParse(orderDetail.Tint, out var orderDetailTint))
        {
            return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
        }

        var cost = connection.Player.MembershipStatus == 0
            ? appStoreBundleDefinition.Price
            : appStoreBundleDefinition.MemberDiscount;

        var totalCostValue = (long)cost * orderDetail.Quantity;

        if (totalCostValue < 0 || totalCostValue > int.MaxValue)
        {
            return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
        }

        var totalCost = (int)totalCostValue;

        if (connection.Player.StationCash < totalCost)
        {
            return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.BalanceNotEnough);
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters
            .Include(x => x.Items)
            .Include(x => x.Mounts)
            .Include(x => x.Pets)
            .AsSplitQuery()
            .SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
        {
            return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
        }

        var lastItemId = dbCharacter.Items.Count > 0 ? dbCharacter.Items.Max(i => i.Id) : 0;
        var lastMountId = dbCharacter.Mounts.Count > 0 ? dbCharacter.Mounts.Max(x => x.Id) : 0;
        var lastPetId = dbCharacter.Pets.Count > 0 ? dbCharacter.Pets.Max(x => x.Id) : 0;

        var pendingUpdates = new List<Action>();

        foreach (var bundleEntry in appStoreBundleDefinition.Entries)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(bundleEntry.MarketingItemId, out var clientItemDefinition))
            {
                return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
            }

            if (clientItemDefinition.Type == 1 || clientItemDefinition.Type == ItemDefinitionHelper.PetClothingItemType
                || clientItemDefinition.Type == ItemDefinitionHelper.PetUtilityItemType || clientItemDefinition.Type == 12
                || clientItemDefinition.Type == ItemDefinitionHelper.HouseItemType)
            {
                var totalQuantity = orderDetail.Quantity * bundleEntry.Quantity;

                if (clientItemDefinition.MaxStackSize > 0
                    && dbCharacter.Items.Where(i => i.Definition == clientItemDefinition.Id && (!appStoreBundleDefinition.IsTintable || i.Tint == orderDetailTint))
                        .Sum(i => i.Count) + totalQuantity > clientItemDefinition.MaxStackSize)
                {
                    return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
                }

                var dbItem = ItemDefinitionHelper.IsStackable(clientItemDefinition)
                    ? dbCharacter.Items.SingleOrDefault(i => i.Definition == clientItemDefinition.Id && i.Tint == orderDetailTint)
                    : null;

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
                    var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == savedItem.Id);

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

                        HousingHelper.NotifyHouseAdded(_resourceManager, _dbContextFactory, connection.Player, itemDefinition, clientItem.Id);
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
            else if (clientItemDefinition.Type == ItemDefinitionHelper.PetItemType)
            {
                if (!PetHelper.TryGetModel(_resourceManager, clientItemDefinition, out var modelDefinition))
                {
                    return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
                }

                var totalQuantityValue = (long)orderDetail.Quantity * bundleEntry.Quantity;

                if (totalQuantityValue > int.MaxValue - lastPetId)
                {
                    return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
                }

                var totalQuantity = (int)totalQuantityValue;

                if (clientItemDefinition.MaxStackSize > 0
                    && dbCharacter.Pets.Count(pet => pet.Definition == clientItemDefinition.Id && (!appStoreBundleDefinition.IsTintable || pet.Tint == orderDetailTint))
                        + totalQuantity > clientItemDefinition.MaxStackSize)
                {
                    return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
                }

                var petTint = appStoreBundleDefinition.IsTintable ? orderDetailTint : clientItemDefinition.Icon.TintId;
                var dbPets = new List<DbPet>();

                for (var i = 0; i < totalQuantity; i++)
                {
                    var dbPet = PetHelper.CreatePet(++lastPetId, clientItemDefinition.Id, petTint);

                    dbCharacter.Pets.Add(dbPet);
                    dbPets.Add(dbPet);
                }

                pendingUpdates.Add(() =>
                {
                    foreach (var dbPet in dbPets)
                        PetHelper.AddPet(connection.Player, _resourceManager, dbPet, clientItemDefinition, modelDefinition);

                    connection.Player.PlayerPet.SendPetList();
                });
            }
            else if (clientItemDefinition.Type == ItemDefinitionHelper.MountItemType)
            {
                if (!_resourceManager.Mounts.TryGetValue(clientItemDefinition.Param1, out var mountDefinition))
                {
                    return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
                }

                var mountTint = appStoreBundleDefinition.IsTintable ? orderDetailTint : mountDefinition.TintId;

                if (dbCharacter.Mounts.Any(x => x.Definition == mountDefinition.Id && x.Tint == mountTint))
                {
                    return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
                }

                var dbMount = new DbMount
                {
                    Id = ++lastMountId,

                    Tint = mountTint,
                    Definition = mountDefinition.Id,
                    IsUpgraded = mountDefinition.IsUpgradable // TODO: Implement training
                };

                dbCharacter.Mounts.Add(dbMount);

                var mountDef = mountDefinition;
                var savedMount = dbMount;

                pendingUpdates.Add(() =>
                {
                    if (!connection.Player.Mounts.Any(x => x.Definition == mountDef.Id && x.TintId == mountTint))
                    {
                        connection.Player.Mounts.Add(new PacketMountInfo
                        {
                            Id = savedMount.Id,
                            Definition = mountDef.Id,
                            NameId = mountDef.NameId,
                            ImageSetId = mountDef.ImageSetId,
                            TintId = mountTint,
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

                    ItemDefinitionHelper.SendItemDefinition(connection.Player, _resourceManager, clientItemDefinition);
                });
            }
            else
            {
                // TODO: Implement other item types

                return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
            }
        }

        dbCharacter.StationCash -= totalCost;

        if (dbContext.SaveChanges() <= 0)
        {
            return SendOrderResult(connection, packetInGamePurchasePlaceOrderResponse, InGamePurchaseOrderResult.OrderFailure);
        }

        connection.Player.StationCash = dbCharacter.StationCash;

        foreach (var pendingUpdate in pendingUpdates)
            pendingUpdate();

        packetInGamePurchasePlaceOrderResponse.Result = InGamePurchaseOrderResult.Success;

        packetInGamePurchasePlaceOrderResponse.Total = totalCost;

        connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);

        return true;
    }

    private static bool SendOrderResult(GatewayConnection connection, PacketInGamePurchasePlaceOrderResponse packetInGamePurchasePlaceOrderResponse,
        InGamePurchaseOrderResult inGamePurchaseOrderResult)
    {
        packetInGamePurchasePlaceOrderResponse.Result = inGamePurchaseOrderResult;
        connection.SendTunneled(packetInGamePurchasePlaceOrderResponse);
        return true;
    }
}