using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

public class RewardManager : IRewardManager
{
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public RewardManager(ILogger<RewardManager> logger, IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
    }

    public bool TryRollReward(string rewardTableKey, out RewardDropDefinition? drop)
    {
        drop = null;

        if (!_resourceManager.RewardTables.TryGetValue(rewardTableKey.Trim().ToLowerInvariant(), out var table))
        {
            _logger.LogError("Unknown reward table {key}.", rewardTableKey);
            return false;
        }

        drop = table.Table.SelectRandom();
        return true;
    }

    public bool TryGrantReward(Player player, RewardDropDefinition drop, ulong sourceGuid = 0)
    {
        return drop switch
        {
            ItemRewardDropDefinition item => TryGrantItem(player, item.ItemDefinitionId,
                item.TintTable?.SelectRandom().TintId ?? 0, sourceGuid),
            CurrencyRewardDropDefinition currency => TryGrantCurrency(player, currency.CurrencyType, currency.Amount),
            _ => false
        };
    }

    public bool TryGrantItem(Player player, int itemDefinitionId, int tint, ulong sourceGuid = 0)
    {
        // NOTE: I think similar logic lives in the shop + collections stuff. We should eventually extract
        // and generalize that to avoid repeats. 
        if (!_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition))
        {
            _logger.LogError("Cannot grant unknown item definition {itemDefinitionId}.", itemDefinitionId);
            return false;
        }

        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var dbCharacter = dbContext.Characters
            .Include(character => character.Items)
            .SingleOrDefault(character => character.Id == characterId);

        if (dbCharacter is null)
            return false;

        var dbItem = dbCharacter.Items.SingleOrDefault(item => item.Definition == itemDefinitionId && item.Tint == tint);

        if (dbItem is null)
        {
            dbItem = new DbItem
            {
                Id = dbCharacter.Items.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
                Definition = itemDefinitionId,
                Count = 1,
                Tint = tint
            };

            dbCharacter.Items.Add(dbItem);
        }
        else
        {
            dbItem.Count++;
        }

        if (dbContext.SaveChanges() <= 0)
            return false;

        var clientItem = player.Items.SingleOrDefault(item => item.Definition == itemDefinitionId && item.Tint == tint);

        if (clientItem is null)
        {
            clientItem = new ClientItem
            {
                Id = dbItem.Id,
                Definition = dbItem.Definition,
                Count = dbItem.Count,
                Tint = dbItem.Tint
            };

            player.Items.Add(clientItem);

            using var writer = new PacketWriter();
            clientItem.Serialize(writer);
            itemDefinition.Serialize(writer);

            player.SendTunneled(new ClientUpdatePacketItemAdd { Payload = writer.Buffer });
        }
        else
        {
            clientItem.Count = dbItem.Count;
            player.SendTunneled(new ClientUpdatePacketItemUpdate
            {
                ItemGuid = clientItem.Id,
                Count = clientItem.Count
            });
        }

        SendRewardToast(player, clientItem, itemDefinition, sourceGuid);

        return true;
    }

    public bool TryGrantCurrency(Player player, CurrencyType currencyType, int amount)
    {
        if (amount <= 0)
            return false;

        if (currencyType == CurrencyType.StationCash)
        {
            _logger.LogWarning("Station Cash grants are not yet implemented.");
            return false;
        }

        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var dbCharacter = dbContext.Characters.SingleOrDefault(character => character.Id == characterId);

        if (dbCharacter is null)
            return false;

        dbCharacter.Coins += amount;

        if (dbContext.SaveChanges() <= 0)
            return false;

        player.Coins = dbCharacter.Coins;

        player.SendTunneled(new ClientUpdatePacketCoinCount { Coins = player.Coins });

        return true;
    }

    private static void SendRewardToast(Player player, ClientItem clientItem, ClientItemDefinition itemDefinition, ulong sourceGuid)
    {
        // TODO: Is there a coin/station cash toast? How about for other types of rewards..?
        var notificationId = Environment.TickCount & int.MaxValue;

        var packet = new RewardBundlePacket
        {
            Unknown8 = itemDefinition.DescriptionId
        };
        packet.RewardBundle.SourceGuid = sourceGuid ^ (uint)notificationId;
        packet.RewardBundle.PlayerGuid = player.Guid;
        packet.RewardBundle.IconId = itemDefinition.Icon.Id;
        packet.RewardBundle.NameId = itemDefinition.NameId;
        packet.RewardBundle.Entries.Add(new RewardBundleEntryItem
        {
            IconId = itemDefinition.Icon.Id,
            NameId = itemDefinition.NameId,
            DefinitionId = clientItem.Definition,
            Tint = clientItem.Tint,
            ItemGuid = clientItem.Id
        });

        player.SendTunneled(packet);
    }
}
