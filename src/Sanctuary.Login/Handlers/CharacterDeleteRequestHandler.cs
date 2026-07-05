using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Login.Services;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class CharacterDeleteRequestHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static CharacterJsonSyncService _characterJsonSyncService = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CharacterDeleteRequestHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _characterJsonSyncService = new CharacterJsonSyncService();
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!CharacterDeleteRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CharacterDeleteRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CharacterDeleteRequest), packet);

        var characterDeleteReply = new CharacterDeleteReply();

        if (connection.UserId == 0)
        {
            characterDeleteReply.Status = 2;
            connection.Send(characterDeleteReply);
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var character = dbContext.Characters.SingleOrDefault(x =>
            x.UserId == connection.UserId &&
            x.Id == GuidHelper.GetPlayerId(packet.EntityKey));

        if (character is null)
        {
            characterDeleteReply.Status = 2;
            connection.Send(characterDeleteReply);
            return true;
        }

        var deletedCharacterName = $"{character.FirstName} {character.LastName}".Trim();
        var deletedUserId = character.UserId;

        var characterId = character.Id;

        var friendLinks = dbContext.Friends
            .Where(x => x.CharacterId == characterId || x.FriendCharacterId == characterId)
            .ToList();

        if (friendLinks.Count > 0)
            dbContext.Friends.RemoveRange(friendLinks);

        var ignoreLinks = dbContext.Ignores
            .Where(x => x.CharacterId == characterId || x.IgnoreCharacterId == characterId)
            .ToList();

        if (ignoreLinks.Count > 0)
            dbContext.Ignores.RemoveRange(ignoreLinks);

        using var transaction = dbContext.Database.BeginTransaction();

        var guildMember = dbContext.GuildMembers
            .AsNoTracking()
            .SingleOrDefault(x => x.Id == characterId);

        if (guildMember is not null)
        {
            character.GuildMemberId = null;

            var deletedGuildMember = dbContext.GuildMembers
                .Where(x => x.Id == characterId)
                .ExecuteDelete();

            if (deletedGuildMember <= 0)
            {
                characterDeleteReply.Status = 2;
                connection.Send(characterDeleteReply);
                return true;
            }

            var hasMembers = dbContext.GuildMembers.Any(x => x.GuildId == guildMember.GuildId);
            if (!hasMembers)
            {
                dbContext.Guilds
                    .Where(x => x.Id == guildMember.GuildId)
                    .ExecuteDelete();
            }
            else if (guildMember.Role == GuildRole.Leader.Id
                     && !dbContext.GuildMembers.Any(x => x.GuildId == guildMember.GuildId && x.Role == GuildRole.Leader.Id))
            {
                var newLeader = dbContext.GuildMembers
                    .Where(x => x.GuildId == guildMember.GuildId)
                    .OrderBy(x => x.Role)
                    .ThenBy(x => x.Joined)
                    .ThenBy(x => x.Id)
                    .FirstOrDefault();

                if (newLeader is not null)
                    newLeader.Role = GuildRole.Leader.Id;
            }

            _logger.LogInformation(
                "Removed deleted character from guild membership. CharacterId: {characterId}, GuildId: {guildId}, Role: {role}",
                characterId,
                guildMember.GuildId,
                guildMember.Role);
        }

        dbContext.Remove(character);

        if (dbContext.SaveChanges() <= 0)
        {
            characterDeleteReply.Status = 2;
            connection.Send(characterDeleteReply);
            return true;
        }

        transaction.Commit();

        try
        {
            _characterJsonSyncService.RemoveCharacterNameForUser(deletedUserId, deletedCharacterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove deleted character from json stores. UserId: {userId}, CharacterName: {characterName}",
                deletedUserId,
                deletedCharacterName);
        }

        characterDeleteReply.Status = 1;
        characterDeleteReply.EntityKey = packet.EntityKey;

        connection.Send(characterDeleteReply);

        return true;
    }
}
