using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Database;
using Sanctuary.Packet.Common;
using Sanctuary.Database.Entities;
using Sanctuary.Core.Configuration;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class CharacterCreateRequestHandler
{
    private static ILogger _logger = null!;
    private static LoginServerOptions _options = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CharacterCreateRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<LoginServerOptions>>();
        _options = options.CurrentValue;
        options.OnChange(o => _options = o);
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!CharacterCreateRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CharacterCreateRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CharacterCreateRequest), packet);

        var characterCreateReply = new CharacterCreateReply();

        if (!CharacterData.TryDeserialize(packet.Payload, out var characterData))
        {
            _logger.LogError("Failed to create character, invalid character data.");

            characterCreateReply.Result = 3;

            connection.Send(characterCreateReply);

            return true;
        }

        if (connection.Guid == 0)
        {
            _logger.LogError("Failed to create character, unknown guid.");

            characterCreateReply.Result = 3;

            connection.Send(characterCreateReply);

            return true;
        }

        // TODO: Validate name against CharacterCreateItemClothingSets.txt, CharacterCreateItemMappings.txt

        if (!_resourceManager.Models.TryGetValue(characterData.Model, out var model))
        {
            _logger.LogError("Failed to create character, invalid character model. {id}", characterData.Model);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        if (!_resourceManager.HeadMappings.TryGetValue(characterData.Head, out var head))
        {
            _logger.LogError("Failed to create character, invalid character head. {id}", characterData.Head);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        if (!_resourceManager.HairMappings.TryGetValue(characterData.Hair, out var hair))
        {
            _logger.LogError("Failed to create character, invalid character hair. {id}", characterData.Hair);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        if (!_resourceManager.SkinToneMappings.TryGetValue(characterData.SkinTone, out var skinTone))
        {
            _logger.LogError("Failed to create character, invalid character skin tone. {id}", characterData.SkinTone);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        string? facePaint = null;

        if (!_resourceManager.FacePaintMappings.TryGetValue(characterData.FacePaint, out facePaint))
        {
            _logger.LogError("Failed to create character, invalid character face paint. {id}", characterData.FacePaint);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        string? modelCustomization = null;

        if (characterData.ModelCustomization > 0 && !_resourceManager.ModelCustomizationMappings.TryGetValue(characterData.ModelCustomization, out modelCustomization))
        {
            _logger.LogError("Failed to create character, invalid character model customization. {id}", characterData.ModelCustomization);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        if (!_resourceManager.ItemDefinitions.TryGetValue(characterData.ItemFeet, out var itemFeet))
        {
            _logger.LogError("Failed to create character, invalid character item feet. {id}", characterData.ItemFeet);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        if (!_resourceManager.ItemDefinitions.TryGetValue(characterData.ItemLegs, out var itemLegs))
        {
            _logger.LogError("Failed to create character, invalid character item legs. {id}", characterData.ItemLegs);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        if (!_resourceManager.ItemDefinitions.TryGetValue(characterData.ItemChest, out var itemChest))
        {
            _logger.LogError("Failed to create character, invalid character item chest. {id}", characterData.ItemChest);

            characterCreateReply.Result = 2;

            connection.Send(characterCreateReply);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var nameTaken = dbContext.Characters.Any(x => x.FirstName == characterData.FirstName && x.LastName == characterData.LastName);

        if (nameTaken)
        {
            _logger.LogInformation("Failed to create character, name taken.");

            characterCreateReply.Result = 4;

            connection.Send(characterCreateReply);

            return true;
        }

        var characterCount = dbContext.Characters.Count(x => x.UserGuid == connection.Guid);
        var maxCharacters = dbContext.Users.Where(x => x.Guid == connection.Guid).Select(x => x.MaxCharacters).SingleOrDefault();

        if (characterCount >= maxCharacters)
        {
            _logger.LogInformation("Failed to create character, max characters.");

            characterCreateReply.Result = 4;

            connection.Send(characterCreateReply);

            return true;
        }

        var dbCharacter = new DbCharacter
        {
            FirstName = characterData.FirstName ?? string.Empty,
            LastName = characterData.LastName ?? string.Empty,
            Model = characterData.Model,
            Head = head,
            Hair = hair,
            ModelCustomization = modelCustomization,
            FacePaint = facePaint,
            SkinTone = skinTone,
            EyeColor = characterData.EyeColor,
            HairColor = characterData.HairColor,

            Gender = model.Gender,

            Position = new Vector4(_options.DefaultPositionX, _options.DefaultPositionY, _options.DefaultPositionZ, 1.0f),
            Rotation = new Quaternion(_options.DefaultRotationX, 0.0f, _options.DefaultRotationZ, 0.0f),

            UserGuid = connection.Guid
        };

        if (_options.DefaultTitleId > 0)
        {
            if (_resourceManager.PlayerTitles.TryGetValue(_options.DefaultTitleId, out var playerTitle))
            {
                var dbCharacterTitle = new DbTitle
                {
                    Id = playerTitle.Id
                };

                dbCharacter.Titles.Add(dbCharacterTitle);

                dbCharacter.ActiveTitleId = playerTitle.Id;
            }
        }

        if (!_resourceManager.Profiles.TryGetValue(_options.DefaultProfileId, out var profileData))
        {
            _logger.LogError("Failed to create character, invalid default profile.");

            characterCreateReply.Result = 3;

            connection.Send(characterCreateReply);

            return true;
        }

        var dbProfile = new DbProfile
        {
            Id = profileData.Id,
            Level = 20
        };

        dbCharacter.ActiveProfileId = dbProfile.Id;

        var chestItem = new DbItem
        {
            Id = dbCharacter.Items.Count + 1,
            Definition = itemChest.Id,
            Tint = characterData.TintChest
        };

        dbCharacter.Items.Add(chestItem);

        var legItem = new DbItem
        {
            Id = dbCharacter.Items.Count + 1,
            Definition = itemLegs.Id,
            Tint = characterData.TintLegs
        };

        dbCharacter.Items.Add(legItem);

        var feetItem = new DbItem
        {
            Id = dbCharacter.Items.Count + 1,
            Definition = itemFeet.Id,
            Tint = characterData.TintFeet
        };

        dbCharacter.Items.Add(feetItem);

        dbProfile.Items.Add(chestItem);
        dbProfile.Items.Add(legItem);
        dbProfile.Items.Add(feetItem);

        dbCharacter.Profiles.Add(dbProfile);

        if (_options.UnlockAllItems)
            UnlockAllItems(dbCharacter);

        if (_options.UnlockAllTitles)
            UnlockAllTitles(dbCharacter);

        if (_options.UnlockAllMounts)
            UnlockAllMounts(dbCharacter);

        if (_options.UnlockAllProfiles)
            UnlockAllProfiles(dbCharacter, chestItem, legItem, feetItem);

        dbContext.Characters.Add(dbCharacter);

        if (dbContext.SaveChanges() > 0)
        {
            characterCreateReply.Result = 1;
            characterCreateReply.Guid = dbCharacter.Guid;
        }

        connection.Send(characterCreateReply);

        return true;
    }

    private static void UnlockAllItems(DbCharacter dbCharacter)
    {
        var items = new List<DbItem>();

        foreach (var itemDefinition in _resourceManager.ItemDefinitions.Values)
        {
            if (itemDefinition.Type != 1 && itemDefinition.Type != 12)
                continue;

            if (itemDefinition.GenderUsage != 0 && itemDefinition.GenderUsage != dbCharacter.Gender)
                continue;

            if (string.IsNullOrEmpty(itemDefinition.ModelName))
                continue;

            if (dbCharacter.Items.Any(x => x.Definition == itemDefinition.Id))
                continue;

            var dbItem = new DbItem
            {
                Id = dbCharacter.Items.Count + 1,
                Definition = itemDefinition.Id,
                Tint = itemDefinition.Icon.TintId
            };

            dbCharacter.Items.Add(dbItem);
        }
    }

    private static void UnlockAllTitles(DbCharacter dbCharacter)
    {
        foreach (var playerTitle in _resourceManager.PlayerTitles.Values)
        {
            if (dbCharacter.Titles.Any(x => x.Id == playerTitle.Id))
                continue;

            var dbCharacterTitle = new DbTitle
            {
                Id = playerTitle.Id
            };

            dbCharacter.Titles.Add(dbCharacterTitle);
        }
    }

    private static void UnlockAllMounts(DbCharacter dbCharacter)
    {
        foreach (var mount in _resourceManager.Mounts.Values)
        {
            var dbMount = new DbMount
            {
                Id = mount.Id,
                IsUpgraded = mount.IsUpgraded
            };

            dbCharacter.Mounts.Add(dbMount);
        }
    }

    private static void UnlockAllProfiles(DbCharacter dbCharacter, DbItem chestItem, DbItem legItem, DbItem feetItem)
    {
        foreach (var profileData in _resourceManager.Profiles.Values)
        {
            if (dbCharacter.Profiles.Any(x => x.Id == profileData.Id))
                continue;

            var dbProfile = new DbProfile
            {
                Id = profileData.Id,
                Level = 20
            };

            // TODO
            if (profileData.DefaultItems.Count == 0)
            {
                dbProfile.Items.Add(chestItem);
                dbProfile.Items.Add(legItem);
                dbProfile.Items.Add(feetItem);
            }
            else
            {
                foreach (var defaultItemId in profileData.DefaultItems)
                {
                    if (!_resourceManager.ItemDefinitions.TryGetValue(defaultItemId, out var defaultItemDefinition))
                        continue;

                    if (defaultItemDefinition.GenderUsage != 0 && defaultItemDefinition.GenderUsage != dbCharacter.Gender)
                        continue;

                    var dbItem = dbCharacter.Items.SingleOrDefault(x => x.Definition == defaultItemId);

                    if (dbItem is null)
                    {
                        dbItem = new DbItem
                        {
                            Id = dbCharacter.Items.Count + 1,
                            Definition = defaultItemDefinition.Id,
                            Tint = defaultItemDefinition.Icon.TintId
                        };

                        dbCharacter.Items.Add(dbItem);
                    }

                    dbProfile.Items.Add(dbItem);
                }
            }

            dbCharacter.Profiles.Add(dbProfile);
        }
    }
}