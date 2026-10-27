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
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CoinStoreSellToClientRequestPacketHandler
{
    private const int NoTintGroupId = 0;
    private const int HouseItemType = 16;

    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IHousingManager _housingManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CoinStoreSellToClientRequestPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _housingManager = serviceProvider.GetRequiredService<IHousingManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CoinStoreSellToClientRequestPacket.TryDeserialize(data, out var coinStoreSellToClientRequestPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CoinStoreSellToClientRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CoinStoreSellToClientRequestPacket), coinStoreSellToClientRequestPacket);

        var coinStoreTransactionCompletePacket = new CoinStoreTransactionCompletePacket
        {
            TransactionRecord =
            {
                Type = CoinStoreTransactionType.Buy
            }
        };

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(coinStoreSellToClientRequestPacket.ItemRecord.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("User tried to buy unknown item definition. {guid} {definition}", connection.Player.Guid, coinStoreSellToClientRequestPacket.ItemRecord.Definition);

            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidItem;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        if (!_resourceManager.CoinStoreItems.TryGetValue(clientItemDefinition.Id, out var coinStoreItem))
        {
            _logger.LogWarning("User tried to buy an item the coin store doesn't sell. {guid} {definition}", connection.Player.Guid, clientItemDefinition.Id);

            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidItem;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        // Mounts and houses are owned, not stacked.
        var isOwnable = clientItemDefinition.Type is ItemDefinitionHelper.MountItemType or HouseItemType;

        if (isOwnable && coinStoreSellToClientRequestPacket.Quantity != 1)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidQuantity;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        // Everyone is a member now that membership no longer exists, and that's the price the client shows.
        var totalCost = clientItemDefinition.GetMemberPurchasePrice() * coinStoreSellToClientRequestPacket.Quantity;

        if (connection.Player.Coins < totalCost)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.NotEnoughCoins;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        var tint = coinStoreSellToClientRequestPacket.ItemRecord.Tint;

        var hasTintGroup = coinStoreItem.TintGroupId != NoTintGroupId;

        if (!hasTintGroup)
            tint = clientItemDefinition.Icon.TintId;

        if (clientItemDefinition.Type == ItemDefinitionHelper.PetItemType)
            return BuyPet(connection, coinStoreSellToClientRequestPacket, coinStoreTransactionCompletePacket, clientItemDefinition, tint, hasTintGroup, totalCost);

        if (clientItemDefinition.Type == ItemDefinitionHelper.MountItemType)
            return BuyMount(connection, coinStoreTransactionCompletePacket, clientItemDefinition, tint, hasTintGroup, totalCost);

        HouseDefinition? houseDefinition = null;

        if (clientItemDefinition.Type == HouseItemType)
        {
            houseDefinition = _resourceManager.Houses.Values
                .FirstOrDefault(x => x.ItemDefinitionIds.Contains(clientItemDefinition.Id));

            // Never charge for a house that has no zone.
            if (houseDefinition is null)
            {
                coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidItem;

                connection.SendTunneled(coinStoreTransactionCompletePacket);

                return true;
            }
        }

        var houseDefinitionId = houseDefinition?.Id ?? 0;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbQuery = dbContext.Characters
            .Where(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid))
            .Select(x => new
            {
                Character = x,
                Item = x.Items.SingleOrDefault(i => i.Definition == clientItemDefinition.Id && i.Tint == tint),
                OwnedCount = x.Items.Where(i => i.Definition == clientItemDefinition.Id && (!hasTintGroup || i.Tint == tint)).Sum(i => (int?)i.Count) ?? 0,
                NextId = x.Items.Max(i => i.Id),
                OwnsHouse = x.Houses.Any(h => h.Definition == houseDefinitionId)
            })
            .SingleOrDefault();

        // Never charge twice for the same house.
        if (dbQuery is null || (houseDefinition is not null && dbQuery.OwnsHouse))
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Error;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        if (clientItemDefinition.MaxStackSize > 0
            && dbQuery.OwnedCount + coinStoreSellToClientRequestPacket.Quantity > clientItemDefinition.MaxStackSize)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidQuantity;

            connection.SendTunneled(coinStoreTransactionCompletePacket);

            return true;
        }

        var dbItem = dbQuery.Item;

        if (dbItem is not null)
        {
            dbItem.Tint = tint;
            dbItem.Count += coinStoreSellToClientRequestPacket.Quantity;
        }
        else
        {
            dbItem = new DbItem
            {
                Id = dbQuery.NextId + 1,
                Definition = clientItemDefinition.Id,
                Tint = tint,

                Count = coinStoreSellToClientRequestPacket.Quantity
            };

            dbQuery.Character.Items.Add(dbItem);
        }

        dbQuery.Character.Coins -= totalCost;

        if (dbContext.SaveChanges() <= 0)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Error;

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

        if (houseDefinition is not null && !_housingManager.TryGrantHouse(connection.Player, houseDefinition, out _))
            _logger.LogError("Failed to grant purchased house {house} to {player}.", houseDefinition.Id, connection.Player.Guid);

        coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Success;

        coinStoreTransactionCompletePacket.ItemGuid = clientItem.Id;

        coinStoreTransactionCompletePacket.TransactionRecord.Type = CoinStoreTransactionType.Buy;

        coinStoreTransactionCompletePacket.TransactionRecord.Id = connection.Player.CoinStoreTransactions.Count + 1;

        coinStoreTransactionCompletePacket.TransactionRecord.Timestamp = DateTimeOffset.UtcNow;

        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Definition = clientItem.Definition;
        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Tint = clientItem.Tint;

        coinStoreTransactionCompletePacket.TransactionRecord.Quantity = coinStoreSellToClientRequestPacket.Quantity;

        connection.SendTunneled(coinStoreTransactionCompletePacket);

        connection.Player.CoinStoreTransactions.Add(coinStoreTransactionCompletePacket.TransactionRecord);

        return true;
    }

    private static bool BuyPet(GatewayConnection connection, CoinStoreSellToClientRequestPacket coinStoreSellToClientRequestPacket,
        CoinStoreTransactionCompletePacket coinStoreTransactionCompletePacket, ClientItemDefinition clientItemDefinition, int tint,
        bool hasTintGroup, int totalCost)
    {
        if (coinStoreSellToClientRequestPacket.Quantity <= 0)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidQuantity;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        if (!PetHelper.TryGetModel(_resourceManager, clientItemDefinition, out var modelDefinition))
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidItem;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();
        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var dbCharacter = dbContext.Characters.SingleOrDefault(character => character.Id == characterId);

        if (dbCharacter is null)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Error;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        if (clientItemDefinition.MaxStackSize > 0
            && dbContext.Pets.Count(pet => pet.CharacterId == characterId && pet.Definition == clientItemDefinition.Id && (!hasTintGroup || pet.Tint == tint))
                + coinStoreSellToClientRequestPacket.Quantity > clientItemDefinition.MaxStackSize)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidQuantity;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        var lastPetId = dbContext.Pets.Where(pet => pet.CharacterId == characterId)
            .Max(pet => (int?)pet.Id) ?? 0;

        if (coinStoreSellToClientRequestPacket.Quantity > int.MaxValue - lastPetId)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Error;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        var pets = new List<DbPet>();

        for (var i = 0; i < coinStoreSellToClientRequestPacket.Quantity; i++)
        {
            var dbPet = PetHelper.CreatePet(++lastPetId, clientItemDefinition.Id, tint);
            dbCharacter.Pets.Add(dbPet);
            pets.Add(dbPet);
        }

        dbCharacter.Coins -= totalCost;

        if (dbContext.SaveChanges() <= 0)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Error;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        foreach (var dbPet in pets)
            PetHelper.AddPet(connection.Player, _resourceManager, dbPet, clientItemDefinition, modelDefinition);

        connection.Player.Coins = dbCharacter.Coins;
        connection.Player.PlayerPet.SendPetList();
        connection.SendTunneled(new ClientUpdatePacketCoinCount { Coins = dbCharacter.Coins });

        coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Success;
        coinStoreTransactionCompletePacket.ItemGuid = pets[0].Id;
        coinStoreTransactionCompletePacket.TransactionRecord.Id = connection.Player.CoinStoreTransactions.Count + 1;
        coinStoreTransactionCompletePacket.TransactionRecord.Timestamp = DateTimeOffset.UtcNow;
        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Definition = clientItemDefinition.Id;
        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Tint = tint;
        coinStoreTransactionCompletePacket.TransactionRecord.Quantity = coinStoreSellToClientRequestPacket.Quantity;
        coinStoreTransactionCompletePacket.TransactionRecord.Price = totalCost;
        connection.SendTunneled(coinStoreTransactionCompletePacket);
        connection.Player.CoinStoreTransactions.Add(coinStoreTransactionCompletePacket.TransactionRecord);

        return true;
    }

    private static bool BuyMount(GatewayConnection connection, CoinStoreTransactionCompletePacket coinStoreTransactionCompletePacket,
        ClientItemDefinition clientItemDefinition, int tint, bool hasTintGroup, int totalCost)
    {
        if (!_resourceManager.Mounts.TryGetValue(clientItemDefinition.Param1, out var mountDefinition))
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.InvalidItem;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        var mountTint = hasTintGroup ? tint : mountDefinition.TintId;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var dbCharacter = dbContext.Characters
            .Include(character => character.Mounts)
            .SingleOrDefault(character => character.Id == characterId);

        // Never charge twice for the same mount.
        if (dbCharacter is null || dbCharacter.Mounts.Any(mount => mount.Definition == mountDefinition.Id && mount.Tint == mountTint))
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Error;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        var dbMount = new DbMount
        {
            Id = dbCharacter.Mounts.Count > 0 ? dbCharacter.Mounts.Max(mount => mount.Id) + 1 : 1,

            Tint = mountTint,
            Definition = mountDefinition.Id,
            IsUpgraded = mountDefinition.IsUpgradable // TODO: Implement training
        };

        dbCharacter.Mounts.Add(dbMount);
        dbCharacter.Coins -= totalCost;

        if (dbContext.SaveChanges() <= 0)
        {
            coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Error;
            connection.SendTunneled(coinStoreTransactionCompletePacket);
            return true;
        }

        connection.Player.Mounts.Add(new PacketMountInfo
        {
            Id = dbMount.Id,
            Definition = mountDefinition.Id,
            NameId = mountDefinition.NameId,
            ImageSetId = mountDefinition.ImageSetId,
            TintId = mountTint,
            MembersOnly = mountDefinition.MembersOnly,
            IsUpgradable = mountDefinition.IsUpgradable,
            IsUpgraded = dbMount.IsUpgraded
        });

        connection.SendTunneled(new PacketMountList { Mounts = connection.Player.Mounts });
        ItemDefinitionHelper.SendItemDefinition(connection.Player, _resourceManager, clientItemDefinition);

        connection.Player.Coins = dbCharacter.Coins;
        connection.SendTunneled(new ClientUpdatePacketCoinCount { Coins = dbCharacter.Coins });

        coinStoreTransactionCompletePacket.Result = CoinStoreTransactionResult.Success;
        coinStoreTransactionCompletePacket.TransactionRecord.Id = connection.Player.CoinStoreTransactions.Count + 1;
        coinStoreTransactionCompletePacket.TransactionRecord.Timestamp = DateTimeOffset.UtcNow;
        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Definition = clientItemDefinition.Id;
        coinStoreTransactionCompletePacket.TransactionRecord.ItemRecord.Tint = mountTint;
        coinStoreTransactionCompletePacket.TransactionRecord.Quantity = 1;
        coinStoreTransactionCompletePacket.TransactionRecord.Price = totalCost;
        connection.SendTunneled(coinStoreTransactionCompletePacket);
        connection.Player.CoinStoreTransactions.Add(coinStoreTransactionCompletePacket.TransactionRecord);

        return true;
    }
}
