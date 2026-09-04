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

namespace Sanctuary.Game.Helpers;

public static class RewardHelper
{
    public static bool TryGrantReward(IResourceManager resourceManager, DatabaseContext dbContext, ILogger logger,
        Player player, string rewardTableKey, ulong sourceGuid = 0)
    {
        if (!resourceManager.RewardTables.TryRoll(rewardTableKey, out var drop))
        {
            logger.LogError("Unknown reward table {key}.", rewardTableKey);
            return false;
        }

        return TryGrantReward(resourceManager, dbContext, logger, player, drop, sourceGuid);
    }

    public static bool TryGrantReward(IResourceManager resourceManager, DatabaseContext dbContext, ILogger logger,
        Player player, RewardDropDefinition drop, ulong sourceGuid = 0)
    {
        return drop switch
        {
            ItemRewardDropDefinition item => TryGrantItem(resourceManager, dbContext, logger, player,
                item.ItemDefinitionId, item.TintTable?.SelectRandom().TintId ?? 0, item.Quantity, sourceGuid),
            CurrencyRewardDropDefinition currency => TryGrantCurrency(dbContext, logger, player, currency.CurrencyType, currency.Amount),
            _ => false
        };
    }

    public static bool TryGrantItem(IResourceManager resourceManager, DatabaseContext dbContext, ILogger logger,
        Player player, int itemDefinitionId, int tint, int quantity, ulong sourceGuid = 0)
    {
        // TODO: I think similar logic lives in the shop + collections stuff. We should eventually extract
        // and generalize that to avoid repeats.
        // https://github.com/Open-Source-Free-Realms/Sanctuary/issues/114
        if (!resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition))
        {
            logger.LogError("Cannot grant unknown item definition {itemDefinitionId}.", itemDefinitionId);
            return false;
        }

        var characterId = GuidHelper.GetPlayerId(player.Guid);

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

        try
        {
            if (dbContext.SaveChanges() <= 0)
                return false;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save granted item {itemDefinitionId} for character {characterId}.", itemDefinitionId, characterId);
            return false;
        }

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

    public static bool TryGrantCurrency(DatabaseContext dbContext, ILogger logger, Player player, CurrencyType currencyType, int amount)
    {
        if (amount <= 0)
            return false;

        if (currencyType == CurrencyType.StationCash)
        {
            logger.LogWarning("Station Cash grants are not yet implemented.");
            return false;
        }

        var characterId = GuidHelper.GetPlayerId(player.Guid);

        var dbCharacter = dbContext.Characters.SingleOrDefault(character => character.Id == characterId);

        if (dbCharacter is null)
            return false;

        dbCharacter.Coins += amount;

        try
        {
            if (dbContext.SaveChanges() <= 0)
                return false;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save granted currency for character {characterId}.", characterId);
            return false;
        }

        player.Coins = dbCharacter.Coins;

        player.SendTunneled(new ClientUpdatePacketCoinCount { Coins = player.Coins });

        return true;
    }

    public static bool TryGrantExperience(IResourceManager resourceManager, DatabaseContext dbContext, ILogger logger,
        Player player, int profileId, int amount)
    {
        if (amount <= 0)
            return false;

        var characterId = GuidHelper.GetPlayerId(player.Guid);

        var dbProfile = dbContext.Profiles
            .SingleOrDefault(profile => profile.CharacterId == characterId && profile.Id == profileId);

        if (dbProfile is null)
            return false;

        if (dbProfile.Level >= resourceManager.RankLevels.Keys.Max())
            return false;

        var oldLevel = dbProfile.Level;

        AccrueExperience(resourceManager, dbProfile, amount);

        try
        {
            if (dbContext.SaveChanges() <= 0)
                return false;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save granted experience for profile {profileId}, character {characterId}.", profileId, characterId);
            return false;
        }

        var clientPcProfile = player.Profiles.SingleOrDefault(profile => profile.Id == profileId);

        // NOTE: This shouldn't happen, so if we don't care we can remove this.
        // It returns 'true', i.e., claims success, because the DATABASE gets updated...
        // The client won't...
        if (clientPcProfile is null)
        {
            logger.LogWarning("Granted experience to profile {profileId} for {guid}, but no matching client profile is loaded.",
                profileId, player.Guid);
            return true;
        }

        clientPcProfile.Rank = dbProfile.Level;
        clientPcProfile.RankPercent = resourceManager.RankLevels.GetRankPercent(dbProfile.Level, dbProfile.LevelXP);

        if (dbProfile.Level > oldLevel)
        {
            SendRankUpNotification(player, clientPcProfile.Id, dbProfile.Level);
            player.OnLevelUp(clientPcProfile);
        }

        SendExperienceUpdate(player, clientPcProfile, dbProfile.LevelXP);

        return true;
    }

    private static void AccrueExperience(IResourceManager resourceManager, DbProfile dbProfile, int amount)
    {
        var maxLevel = resourceManager.RankLevels.Keys.Max(); // NOTE: May just want a hard-coded const of '20'.

        dbProfile.LevelXP += amount;

        // TODO: Double check that these conditions are satisfactory for rank-ups.
        while (dbProfile.Level < maxLevel &&
            resourceManager.RankLevels.TryGetValue(dbProfile.Level, out var rankLevel) &&
            dbProfile.LevelXP >= rankLevel.StarsToNextLevel)
        {
            dbProfile.LevelXP -= rankLevel.StarsToNextLevel;
            dbProfile.Level++;
        }
    }

    // Sent on every grant, level-up or not - refreshes the client's profile datasource
    // (XP bar/percent/stars) live, with no fanfare.
    private static void SendExperienceUpdate(Player player, ClientPcProfile profile, int levelXp)
    {
        player.SendTunneled(new ClientUpdatePacketUpdateProfileExperience
        {
            ProfileId = profile.Id,
            Rank = profile.Rank,
            RankPercent = profile.RankPercent,
            StarsAvailable = 0,
            StarsEarned = levelXp,
            Unknown6 = 0
        });
    }

    private static void SendRankUpNotification(Player player, int profileId, int newLevel)
    {
        player.SendTunneled(new ClientUpdatePacketUpdateProfileRank
        {
            ProfileId = profileId,
            Rank = newLevel
        });
    }

    private static void SendRewardToast(Player player, ClientItem clientItem, ClientItemDefinition itemDefinition, int quantity, ulong sourceGuid)
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
            Quantity = quantity,
            ItemGuid = clientItem.Id
        });

        player.SendTunneled(packet);
    }
}
