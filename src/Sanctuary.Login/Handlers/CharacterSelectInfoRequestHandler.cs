using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Packet.Common;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class CharacterSelectInfoRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CharacterSelectInfoRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(LoginConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(CharacterSelectInfoRequest));

        var characterSelectInfoReply = new CharacterSelectInfoReply();

        if (connection.Guid == 0)
        {
            connection.Send(characterSelectInfoReply);

            return false;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var characters = dbContext.Characters
            .AsNoTracking()
            .Where(x => x.UserGuid == connection.Guid)
            .Include(x => x.Profiles)
                .ThenInclude(x => x.Items)
            .AsSplitQuery()
            .ToList();

        characterSelectInfoReply.Status = characters.Count == 0 ? 2 : 1;

        foreach (var character in characters)
        {
            var accountInfoCharacterDetails = new AccountInfoCharacterDetails
            {
                HeadshotUrl = "headshot.png",
                Data =
                {
                    Guid = character.Guid,
                    Name = string.IsNullOrEmpty(character.LastName)
                         ? character.FirstName
                         : $"{character.FirstName} {character.LastName}",
                    Model = character.Model,
                    Head = character.Head,
                    Hair = character.Hair,
                    ModelCustomization = character.ModelCustomization,
                    FacePaint = character.FacePaint,
                    SkinTone = character.SkinTone,
                    EyeTint = character.EyeColor,
                    HairTint = character.HairColor
                }
            };

            var firstProfile = character.Profiles.FirstOrDefault();

            if (firstProfile is not null)
            {
                foreach (var item in firstProfile.Items)
                {
                    if (!_resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var clientItemDefinition))
                        continue;

                    accountInfoCharacterDetails.Data.CharacterAttachments.Add(new CharacterAttachmentData
                    {
                        ModelName = clientItemDefinition.ModelName,
                        TextureAlias = clientItemDefinition.TextureAlias,
                        TintAlias = clientItemDefinition.TintAlias,
                        TintId = item.Tint,
                        CompositeEffectId = clientItemDefinition.CompositeEffectId,
                        Slot = clientItemDefinition.Slot,
                    });
                }
            }

            var entityDetails = new EntityDetails
            {
                EntityKey = character.Guid,
                Status = 1,
                ApplicationData = accountInfoCharacterDetails.Serialize()
            };

            characterSelectInfoReply.Entities.Add(entityDetails);
        }

        connection.Send(characterSelectInfoReply);

        return true;
    }
}