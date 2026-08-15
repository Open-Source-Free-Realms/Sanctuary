using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Packet;
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
        if (!CoinStoreBuyFromClientRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CoinStoreBuyFromClientRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CoinStoreBuyFromClientRequestPacket), packet);

        var coinStoreTransactionCompletePacket = new CoinStoreTransactionCompletePacket
        {
            TransactionRecord =
            {
                Type = 2 // Sell
            }
        };

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == packet.ItemGuid);

        if (clientItem is null)
        {
            coinStoreTransactionCompletePacket.Result = 3;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        var sellQuantity = packet.Quantity <= 0 ? clientItem.Count : packet.Quantity;

        if (clientItem.Count < sellQuantity)
        {
            coinStoreTransactionCompletePacket.Result = 4;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            coinStoreTransactionCompletePacket.Result = 3;

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
            coinStoreTransactionCompletePacket.Result = 3;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        var dbItem = dbQuery.Item;

        var deleteItem = clientItem.Count == sellQuantity;

        if (deleteItem)
            dbContext.Items.Remove(dbItem);
        else
            dbItem.Count -= sellQuantity;

        dbQuery.Character.Coins += clientItemDefinition.ResellValue * sellQuantity;

        if (dbContext.SaveChanges() <= 0)
        {
            coinStoreTransactionCompletePacket.Result = 8;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        if (deleteItem)
            connection.Player.Items.Remove(clientItem);
        else
            clientItem.Count = dbItem.Count;

        connection.Player.Coins = dbQuery.Character.Coins;

        if (deleteItem)
        {
            var clientUpdatePacketItemDelete = new ClientUpdatePacketItemDelete
            {
                ItemGuid = packet.ItemGuid
            };

            connection.SendTunneled(clientUpdatePacketItemDelete);
        }
        else
        {
            var clientUpdatePacketItemUpdate = new ClientUpdatePacketItemUpdate
            {
                ItemGuid = packet.ItemGuid,
                Count = clientItem.Count,
            };

            connection.SendTunneled(clientUpdatePacketItemUpdate);
        }

        var clientUpdatePacketCoinCount = new ClientUpdatePacketCoinCount
        {
            Coins = dbQuery.Character.Coins
        };

        connection.SendTunneled(clientUpdatePacketCoinCount);

        coinStoreTransactionCompletePacket.Result = 1;

        coinStoreTransactionCompletePacket.ItemGuid = clientItem.Id;

        coinStoreTransactionCompletePacket.TransactionRecord.Type = 2; // Sell

        coinStoreTransactionCompletePacket.TransactionRecord.Id = connection.Player.CoinStoreTransactions.Count + 1;

        coinStoreTransactionCompletePacket.TransactionRecord.Timestamp = DateTimeOffset.UtcNow;

        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Definition = clientItem.Definition;
        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Tint = clientItem.Tint;

        coinStoreTransactionCompletePacket.TransactionRecord.Quantity = sellQuantity;

        connection.SendTunneled(coinStoreTransactionCompletePacket);

        connection.Player.CoinStoreTransactions.Add(coinStoreTransactionCompletePacket.TransactionRecord);

        return true;
    }
}
