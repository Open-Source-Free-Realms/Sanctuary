using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class CharacterDeleteRequestHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CharacterDeleteRequestHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
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

        var character = dbContext.Characters.SingleOrDefault(x => x.UserId == connection.UserId && x.Id == GuidHelper.GetPlayerId(packet.EntityKey));

        if (character is null)
        {
            characterDeleteReply.Status = 2;

            connection.Send(characterDeleteReply);

            return true;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var committed = strategy.Execute(() =>
        {
            using var transaction = dbContext.Database.BeginTransaction();

            try
            {
                var guildMember = dbContext.GuildMembers
                    .AsNoTracking()
                    .SingleOrDefault(x => x.Id == character.Id);

                if (guildMember is not null)
                {
                    character.GuildMemberId = null;

                    var deletedGuildMember = dbContext.GuildMembers
                        .Where(x => x.Id == character.Id)
                        .ExecuteDelete();

                    if (deletedGuildMember <= 0)
                        return false;

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
                }

                // Delete references first to avoid foreign key constraint violations.
                var characterId = character.Id;

                var friends = dbContext.Friends.Where(f =>
                    f.CharacterId == characterId || f.FriendCharacterId == characterId);

                var ignores = dbContext.Ignores.Where(i =>
                    i.CharacterId == characterId || i.IgnoreCharacterId == characterId);

                dbContext.Friends.RemoveRange(friends);
                dbContext.Ignores.RemoveRange(ignores);
                dbContext.Remove(character);

                if (dbContext.SaveChanges() == 0)
                    return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to delete character {CharacterId}.", character.Id);

                return false;
            }

            transaction.Commit();
            return true;
        });

        if (!committed)
        {
            characterDeleteReply.Status = 2;

            connection.Send(characterDeleteReply);

            return true;
        }

        characterDeleteReply.Status = 1;
        characterDeleteReply.EntityKey = packet.EntityKey;

        connection.Send(characterDeleteReply);

        return true;
    }
}
