using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public enum SpecialProfileIds
{
    Referee = 58,
    // Enforcer = ??
}

public static class ProfileHelper
{
    public static void GrantDefaultItems(DbCharacter character, DbProfile dbProfile,
        ClientProfileData profileData, IResourceManager resourceManager)
    {
        foreach (var defaultItemId in profileData.DefaultItems)
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(defaultItemId, out var defaultClientItemDefinition))
                continue;

            if (defaultClientItemDefinition.GenderUsage != 0 && defaultClientItemDefinition.GenderUsage != character.Gender)
                continue;

            var dbItem = character.Items.FirstOrDefault(x => x.Definition == defaultItemId);

            if (dbItem is null)
            {
                dbItem = new DbItem
                {
                    Id = character.Items.Count > 0 ? character.Items.Max(x => x.Id) + 1 : 1,
                    CharacterId = character.Id,
                    Definition = defaultClientItemDefinition.Id,
                    Tint = defaultClientItemDefinition.Icon.TintId,
                    Count = 1
                };

                character.Items.Add(dbItem);
            }

            dbProfile.Items.Add(dbItem);
        }
    }

    public static bool AddSpecialProfile(DbCharacter character, DatabaseContext dbContext,
        IResourceManager resourceManager, ILogger logger, SpecialProfileIds profileId)
    {
        int id = (int)profileId;

        if (!resourceManager.Profiles.TryGetValue(id, out var profileData))
        {
            logger.LogWarning("Profile with ID {profileId} does not exist in the resource manager.", id);
            return false;
        }

        // Check if the character already has the profile
        if (character.Profiles.Any(p => p.Id == id))
        {
            logger.LogDebug("Character {characterId} already has profile {profileId}.", character.Id, id);
            return false;
        }

        DbProfile newProfile = new DbProfile
        {
            CharacterId = character.Id,
            Id = id,
            Level = 20
        };

        var existingItemIds = character.Items.Select(x => x.Id).ToHashSet();

        GrantDefaultItems(character, newProfile, profileData, resourceManager);
        dbContext.Attach(character);

        foreach (var item in character.Items)
        {
            if (!existingItemIds.Contains(item.Id))
                dbContext.Entry(item).State = EntityState.Added;
        }

        dbContext.Entry(newProfile).State = EntityState.Added;

        try
        {
            if (dbContext.SaveChanges() <= 0)
            {
                logger.LogWarning("Failed to save profile {profileId} for character {characterId}.", id, character.Id);
                return false;
            }
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save profile {profileId} for character {characterId}.", id, character.Id);
            return false;
        }
        character.Profiles.Add(newProfile);
        return true;
    }

    public static bool RemoveSpecialProfile(DbCharacter character, DatabaseContext dbContext, ILogger logger, SpecialProfileIds profileId)
    {
        int id = (int)profileId;

        if (!character.Profiles.Any(p => p.Id == id))
            return true;

        try
        {
            if (dbContext.Profiles.Where(p => p.CharacterId == character.Id && p.Id == id).ExecuteDelete() <= 0)
            {
                logger.LogWarning("Failed to remove profile {profileId} for character {characterId}.", id, character.Id);
                return false;
            }
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to remove profile {profileId} for character {characterId}.", id, character.Id);
            return false;
        }

        var profileToRemove = character.Profiles.FirstOrDefault(p => p.Id == id);
        if (profileToRemove != null)
        {
            character.Profiles.Remove(profileToRemove);
        }

        return true;
    }
}
