using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
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

    public bool TryRollRewardUnowned(Player player, string rewardTableKey, out RewardDropDefinition? drop, out int tint)
    {
        drop = null;
        tint = 0;

        if (!_resourceManager.RewardTables.TryGetValue(rewardTableKey.Trim().ToLowerInvariant(), out var table))
        {
            _logger.LogError("Unknown reward table {key}.", rewardTableKey);
            return false;
        }

        var winnable = table.DropTable.Where(x => IsWinnable(player, x)).ToList();

        if (winnable.Count == 0)
        {
            _logger.LogWarning("{player} owns everything in {key} but tried to roll a reward.", player.Name.FullName, rewardTableKey);
            return false;
        }

        drop = new WeightedDropTable<RewardDropDefinition>(winnable).SelectRandom();

        if (drop is ItemRewardDropDefinition item)
            tint = PickTint(player, item);

        return true;
    }

    private int PickTint(Player player, ItemRewardDropDefinition item)
    {
        if (!_resourceManager.ClientItemDefinitions.TryGetValue(item.ItemDefinitionId, out var itemDefinition))
            return 0;

        if (itemDefinition.Type != ItemDefinitionHelper.MountItemType)
        {
            if (item.Tints.Count == 0)
            {
                if (item.TintGroupId != 0
                    && _resourceManager.TintGroups.TryGetValue(item.TintGroupId, out var groupTints)
                    && groupTints.Count > 0)
                    return groupTints[Random.Shared.Next(groupTints.Count)];

                return 0;
            }

            return new WeightedDropTable<TintDropDefinition>(item.Tints).SelectRandom().TintId;
        }

        if (item.Tints.Count == 0)
            return GetDefaultMountTint(itemDefinition.Param1);

        var unownedTints = item.Tints.Where(x => !OwnsMount(player, itemDefinition.Param1, x.TintId)).ToList();

        return new WeightedDropTable<TintDropDefinition>(unownedTints).SelectRandom().TintId;
    }

    public bool TryGrantReward(Player player, RewardDropDefinition drop, ulong sourceGuid = 0)
    {
        return drop switch
        {
            ItemRewardDropDefinition item => TryGrantItem(player, item.ItemDefinitionId,
                item.TintTable?.SelectRandom().TintId ?? 0, item.Quantity, sourceGuid),
            CurrencyRewardDropDefinition currency => TryGrantCurrency(player, currency.CurrencyType, currency.Amount),
            _ => false
        };
    }

    public bool TryGrantItem(Player player, int itemDefinitionId, int tint, int quantity, ulong sourceGuid = 0)
    {
        // TODO: I think similar logic lives in the shop + collections stuff. We should eventually extract
        // and generalize that to avoid repeats. 
        // https://github.com/Open-Source-Free-Realms/Sanctuary/issues/114
        if (!_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition))
        {
            _logger.LogError("Cannot grant unknown item definition {itemDefinitionId}.", itemDefinitionId);
            return false;
        }

        if (itemDefinition.Type == ItemDefinitionHelper.PetItemType)
        {
            if (!TryGrantPet(player, itemDefinition, tint))
                return false;

            SendMountRewardToast(player, itemDefinition, tint, quantity, sourceGuid);
            return true;
        }

        if (itemDefinition.Type == ItemDefinitionHelper.MountItemType)
        {
            if (!TryGrantMount(player, itemDefinition.Param1, tint))
                return false;

            SendMountRewardToast(player, itemDefinition, tint, quantity, sourceGuid);
            return true;
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
                Count = quantity,
                Tint = tint
            };

            dbCharacter.Items.Add(dbItem);
        }
        else
        {
            dbItem.Count += quantity;
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

        SendRewardToast(player, clientItem, itemDefinition, quantity, sourceGuid);

        return true;
    }

    private bool TryGrantMount(Player player, int mountDefinitionId, int tint)
    {
        if (!_resourceManager.Mounts.TryGetValue(mountDefinitionId, out var mountDefinition))
        {
            _logger.LogError("Cannot grant unknown mount definition of {mountDefinitionId}.", mountDefinitionId);
            return false;
        }

        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var dbCharacter = dbContext.Characters
            .Include(character => character.Mounts)
            .SingleOrDefault(character => character.Id == characterId);
        if (dbCharacter is null)
            return false;
        if (dbCharacter.Mounts.Any(x => x.Definition == mountDefinitionId && x.Tint == tint))
        {
            _logger.LogWarning("Cannot grant duplicate mount definition of {mountDefinitionId} with tint {tint}, {player} already owns one.", mountDefinitionId, tint, player.Name.FullName);
            return false;
        }

        var lastMountId = dbCharacter.Mounts.Count > 0 ? dbCharacter.Mounts.Max(x => x.Id) : 0;

        var dbMount = new DbMount
        {
            Id = ++lastMountId,

            Tint = tint,
            Definition = mountDefinitionId,
            IsUpgraded = mountDefinition.IsUpgradable // TODO: Implement training
        };

        dbCharacter.Mounts.Add(dbMount);
        if (dbContext.SaveChanges() <= 0)
            return false;

        player.Mounts.Add(new PacketMountInfo
        {
            Id = dbMount.Id,
            Definition = mountDefinitionId,
            NameId = mountDefinition.NameId,
            ImageSetId = mountDefinition.ImageSetId,
            TintId = tint,
            TintAlias = mountDefinition.TintAlias,
            MembersOnly = mountDefinition.MembersOnly,
            IsUpgradable = mountDefinition.IsUpgradable,
            IsUpgraded = dbMount.IsUpgraded
        }
            );

        var packetMountList = new PacketMountList
        {
            Mounts = player.Mounts
        };

        player.SendTunneled(packetMountList);
        return true;
    }

    private bool IsWinnable(Player player, RewardDropDefinition drop)
    {
        if (drop is not ItemRewardDropDefinition item)
            return true;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(item.ItemDefinitionId, out var itemDefinition))
        {
            return false;
        }

        if (itemDefinition.GenderUsage != 0 && itemDefinition.GenderUsage != player.Gender)
            return false;

        if (itemDefinition.Type == ItemDefinitionHelper.PetItemType)
            return !OwnsPet(player, itemDefinition.Id);

        if (itemDefinition.Type != ItemDefinitionHelper.MountItemType)
            return true;

        if (item.Tints.Count == 0)
            return !OwnsMount(player, itemDefinition.Param1, GetDefaultMountTint(itemDefinition.Param1));

        return item.Tints.Any(x => !OwnsMount(player, itemDefinition.Param1, x.TintId));
    }
    private bool TryGrantPet(Player player, ClientItemDefinition itemDefinition, int tint)
    {
        if (!PetHelper.TryGetModel(_resourceManager, itemDefinition, out var modelDefinition))
        {
            _logger.LogError("Cannot grant pet {itemDefinitionId}, no model found.", itemDefinition.Id);
            return false;
        }

        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var dbCharacter = dbContext.Characters
            .Include(character => character.Pets)
            .SingleOrDefault(character => character.Id == characterId);

        if (dbCharacter is null)
            return false;

        if (dbCharacter.Pets.Any(x => x.Definition == itemDefinition.Id))
        {
            _logger.LogWarning("Cannot grant duplicate pet {itemDefinitionId}, {player} already owns one.", itemDefinition.Id, player.Name.FullName);
            return false;
        }

        var lastPetId = dbCharacter.Pets.Count > 0 ? dbCharacter.Pets.Max(x => x.Id) : 0;
        var petTint = tint != 0 ? tint : itemDefinition.Icon.TintId;
        var dbPet = PetHelper.CreatePet(++lastPetId, itemDefinition.Id, petTint);

        dbCharacter.Pets.Add(dbPet);

        if (dbContext.SaveChanges() <= 0)
            return false;

        PetHelper.AddPet(player, _resourceManager, dbPet, itemDefinition, modelDefinition);
        player.PlayerPet.SendPetList();

        return true;
    }
    private int GetDefaultMountTint(int mountDefinitionId)
    {
        if (!_resourceManager.Mounts.TryGetValue(mountDefinitionId, out var mountDefinition))
            return 0;

        return mountDefinition.TintId;
    }
    private static bool OwnsMount(Player player, int mountDefinitionId, int tint)
    {
        return player.Mounts.Any(x => x.Definition == mountDefinitionId && x.TintId == tint);
    }
    private static bool OwnsPet(Player player, int petItemDefinitionId)
    {
        return player.PetDefinitions.ContainsValue(petItemDefinitionId);
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

    private static void SendRewardToast(Player player, ClientItem clientItem, ClientItemDefinition itemDefinition, int quantity, ulong sourceGuid)
    {
        // TODO: Is there a coin/station cash toast? How about for other types of rewards..?
        var notificationId = Environment.TickCount & int.MaxValue;

        var packet = new RewardBundlePacket();
        packet.RewardBundle.DescriptionId = itemDefinition.DescriptionId;
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
            Quantity = quantity,
            ItemGuid = clientItem.Id
        });

        player.SendTunneled(packet);
    }
    private static void SendMountRewardToast(Player player, ClientItemDefinition itemDefinition, int tint, int quantity, ulong sourceGuid)
    //Answer to TODO: Yes, but we only know this one right now. Unknown1 appears to be part of Coins & XP Toasts. -Erwynne
    {
        var clientItemDefinitions = new List<ClientItemDefinition> { itemDefinition };

        using var writer = new PacketWriter();

        writer.Write(clientItemDefinitions);

        var playerUpdatePacketItemDefinitions = new PlayerUpdatePacketItemDefinitions();

        playerUpdatePacketItemDefinitions.Payload = writer.Buffer;

        player.SendTunneled(playerUpdatePacketItemDefinitions);

        var notificationId = Environment.TickCount & int.MaxValue;

        var packet = new RewardBundlePacket();
        packet.RewardBundle.DescriptionId = itemDefinition.DescriptionId;
        packet.RewardBundle.SourceGuid = sourceGuid ^ (uint)notificationId;
        packet.RewardBundle.PlayerGuid = player.Guid;
        packet.RewardBundle.IconId = itemDefinition.Icon.Id;
        packet.RewardBundle.NameId = itemDefinition.NameId;
        packet.RewardBundle.Unknown3 = 16; //16 gives a mystery box panel, which doesn't need to be for a classic item the way an item is an item is an item
        packet.RewardBundle.Entries.Add(new RewardBundleEntryItem
        {
            IconId = itemDefinition.Icon.Id,
            NameId = itemDefinition.NameId,
            DefinitionId = itemDefinition.Id,
            TintId = itemDefinition.Icon.TintId,
            Quantity = quantity,
        });

        player.SendTunneled(packet);
    }
}
