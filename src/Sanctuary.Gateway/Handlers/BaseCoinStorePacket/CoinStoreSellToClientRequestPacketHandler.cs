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
public static class CoinStoreSellToClientRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CoinStoreSellToClientRequestPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CoinStoreSellToClientRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CoinStoreSellToClientRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CoinStoreSellToClientRequestPacket), packet);

        var coinStoreTransactionCompletePacket = new CoinStoreTransactionCompletePacket
        {
            TransactionRecord =
            {
                Type = 1 // Buy
            }
        };

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(packet.ItemRecord.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("User tried to buy unknown item definition. {guid} {definition}", connection.Player.Guid, packet.ItemRecord.Definition);

            coinStoreTransactionCompletePacket.Result = 3;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        // Buyable item types. All three are granted the same way - a plain inventory DbItem -
        // so they share this path:
        // 1 = equipment / wearables (the original captured merchant wares)
        // 5 = recipes (chef/blacksmith merchants sell these; e.g. "Crispy Choychoy Delight
        // Recipe"). Without 5 here, ~15 job-merchant wares clicked "Buy" and hard-failed
        // with Result 8.
        // 12 = consumables
        // Types that need special grant handling (mounts, houses, bundles) are still rejected.
        if (clientItemDefinition.Type != 1 &&
            clientItemDefinition.Type != 5 &&
            clientItemDefinition.Type != 12)
        {
            coinStoreTransactionCompletePacket.Result = 8;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        // Merchant wares carry their configured price on the definition itself (ShopInteraction
        // writes def.Cost = the store cost before pushing it), so the standard member/non-member
        // pricing below already charges the configured cost - consistent with what the window shows.
        var cost = connection.Player.MembershipStatus == 0
            ? clientItemDefinition.Cost
            : clientItemDefinition.GetMemberPurchasePrice();

        var totalCost = cost * packet.Quantity;

        if (connection.Player.Coins < totalCost)
        {
            coinStoreTransactionCompletePacket.Result = 7;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        var tint = packet.ItemRecord.Tint;

        if (!clientItemDefinition.IsTintable)
            tint = clientItemDefinition.Icon.TintId;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbQuery = dbContext.Characters
            .Where(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid))
            .Select(x => new
            {
                Character = x,
                Item = x.Items.SingleOrDefault(i => i.Definition == clientItemDefinition.Id && i.Tint == tint),
                // Guard the empty-inventory case: EF translates Max over no rows to SQL
                // MAX() = NULL, which throws when materialized into non-nullable int. A
                // player with zero items (fresh char, or sold everything) buying an item
                // would otherwise crash here. Matches the guarded pattern in ItemRewards.
                NextId = x.Items.Any() ? x.Items.Max(i => i.Id) : 0
            })
            .SingleOrDefault();

        if (dbQuery is null)
        {
            coinStoreTransactionCompletePacket.Result = 8;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        var dbItem = dbQuery.Item;

        if (dbItem is not null)
        {
            dbItem.Tint = tint;
            dbItem.Count += packet.Quantity;
        }
        else
        {
            dbItem = new DbItem
            {
                Id = dbQuery.NextId + 1,
                Definition = clientItemDefinition.Id,
                Tint = tint,

                Count = packet.Quantity
            };

            dbQuery.Character.Items.Add(dbItem);
        }

        dbQuery.Character.Coins -= totalCost;

        if (dbContext.SaveChanges() <= 0)
        {
            coinStoreTransactionCompletePacket.Result = 8;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Definition == clientItemDefinition.Id && x.Tint == tint);

        var addItem = false;

        if (clientItem is not null)
        {
            clientItem.Tint = dbItem.Tint;
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

        connection.Player.Coins = dbQuery.Character.Coins;

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

        var clientUpdatePacketCoinCount = new ClientUpdatePacketCoinCount
        {
            Coins = connection.Player.Coins
        };

        connection.SendTunneled(clientUpdatePacketCoinCount);

        coinStoreTransactionCompletePacket.Result = 1;

        coinStoreTransactionCompletePacket.ItemGuid = clientItem.Id;

        coinStoreTransactionCompletePacket.TransactionRecord.Type = 1; // Buy

        coinStoreTransactionCompletePacket.TransactionRecord.Id = connection.Player.CoinStoreTransactions.Count + 1;

        coinStoreTransactionCompletePacket.TransactionRecord.Timestamp = DateTimeOffset.UtcNow;

        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Definition = clientItem.Definition;
        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Tint = clientItem.Tint;

        coinStoreTransactionCompletePacket.TransactionRecord.Quantity = packet.Quantity;

        connection.SendTunneled(coinStoreTransactionCompletePacket);

        connection.Player.CoinStoreTransactions.Add(coinStoreTransactionCompletePacket.TransactionRecord);

        return true;
    }
}
