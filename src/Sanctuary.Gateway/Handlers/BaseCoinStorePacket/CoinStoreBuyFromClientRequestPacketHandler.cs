using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CoinStoreBuyFromClientRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CoinStoreBuyFromClientRequestPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CoinStoreBuyFromClientRequestPacket.TryDeserialize(data, out var coinStoreBuyFromClientRequestPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CoinStoreBuyFromClientRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CoinStoreBuyFromClientRequestPacket), coinStoreBuyFromClientRequestPacket);

        var coinStoreTransactionCompletePacket = new CoinStoreTransactionCompletePacket
        {
            TransactionRecord =
            {
                Type = CoinStoreTransactionType.Sell
            }
        };

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == coinStoreBuyFromClientRequestPacket.ItemGuid);

        if (clientItem is null)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidItem;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        if (clientItem.Count < coinStoreBuyFromClientRequestPacket.Quantity)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidQuantity;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidItem;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbQuery = dbContext.Characters
            .Where(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid))
            .Select(x => new
            {
                Character = x,
                Item = x.Items.SingleOrDefault(i => i.Id == clientItem.Id)
            })
            .SingleOrDefault();

        if (dbQuery?.Item is null)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidItem;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        var dbItem = dbQuery.Item;

        var deleteItem = clientItem.Count == coinStoreBuyFromClientRequestPacket.Quantity;

        var isPetItem = clientItemDefinition.Type == ItemDefinitionHelper.PetClothingItemType || clientItemDefinition.Type == ItemDefinitionHelper.PetUtilityItemType;

        if (deleteItem)
        {
            if (isPetItem)
            {
                foreach (var dbPet in dbContext.Pets.Where(pet => pet.CharacterId == dbQuery.Character.Id))
                    PetHelper.RemoveItem(dbPet, clientItem.Id);
            }

            dbContext.Items.Remove(dbItem);
        }
        else
            dbItem.Count -= coinStoreBuyFromClientRequestPacket.Quantity;

        dbQuery.Character.Coins += clientItemDefinition.ResellValue * coinStoreBuyFromClientRequestPacket.Quantity;

        if (dbContext.SaveChanges() <= 0)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Error;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        if (deleteItem)
        {
            connection.Player.Items.Remove(clientItem);

            if (isPetItem)
                PetHelper.RemoveItem(connection.Player, clientItem.Id, clientItemDefinition.Slot);
        }
        else
            clientItem.Count = dbItem.Count;

        connection.Player.Coins = dbQuery.Character.Coins;

        if (deleteItem)
        {
            var clientUpdatePacketItemDelete = new ClientUpdatePacketItemDelete
            {
                ItemGuid = coinStoreBuyFromClientRequestPacket.ItemGuid
            };

            connection.SendTunneled(clientUpdatePacketItemDelete);
        }
        else
        {
            var clientUpdatePacketItemUpdate = new ClientUpdatePacketItemUpdate
            {
                ItemGuid = coinStoreBuyFromClientRequestPacket.ItemGuid,
                Count = clientItem.Count,
            };

            connection.SendTunneled(clientUpdatePacketItemUpdate);
        }

        var clientUpdatePacketCoinCount = new ClientUpdatePacketCoinCount
        {
            Coins = dbQuery.Character.Coins
        };

        connection.SendTunneled(clientUpdatePacketCoinCount);

        coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Success;

        coinStoreTransactionCompletePacket.ItemGuid = clientItem.Id;

        coinStoreTransactionCompletePacket.TransactionRecord.Type = CoinStoreTransactionType.Sell;

        coinStoreTransactionCompletePacket.TransactionRecord.Id = connection.Player.CoinStoreTransactions.Count + 1;

        coinStoreTransactionCompletePacket.TransactionRecord.Timestamp = DateTimeOffset.UtcNow;

        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Definition = clientItem.Definition;
        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Tint = clientItem.Tint;

        coinStoreTransactionCompletePacket.TransactionRecord.Quantity = coinStoreBuyFromClientRequestPacket.Quantity;

        connection.SendTunneled(coinStoreTransactionCompletePacket);

        connection.Player.CoinStoreTransactions.Add(coinStoreTransactionCompletePacket.TransactionRecord);

        return true;
    }
}