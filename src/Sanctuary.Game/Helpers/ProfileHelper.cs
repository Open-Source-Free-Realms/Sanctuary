using System.Linq;

using Sanctuary.Database.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class ProfileHelper
{
    /// <summary>
    /// Grants a character the items listed in a profile's <see cref="ClientProfileData.DefaultItems"/>,
    /// reusing any matching item the character already owns, and attaches them to the given profile.
    /// </summary>
    public static void GrantDefaultItems(DbCharacter character, DbProfile dbProfile,
        ClientProfileData profileData, IResourceManager resourceManager)
    {
        foreach (var defaultItemId in profileData.DefaultItems)
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(defaultItemId, out var defaultClientItemDefinition))
                continue;

            if (defaultClientItemDefinition.GenderUsage != 0 && defaultClientItemDefinition.GenderUsage != character.Gender)
                continue;

            var dbItem = character.Items.SingleOrDefault(x => x.Definition == defaultItemId);

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
}
