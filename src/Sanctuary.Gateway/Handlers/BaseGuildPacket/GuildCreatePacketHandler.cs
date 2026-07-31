using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Gateway.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildCreatePacketHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildCreatePacketHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildCreatePacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildCreatePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildCreatePacket), packet);

        var guildName = GuildHelper.NormalizeName(packet.TemporaryName ?? packet.Name);

        _logger.LogTrace(
            "Guild create name fields. Name: \"{name}\", TemporaryName: \"{temporaryName}\", Locale: \"{locale}\", Normalized: \"{normalized}\"",
            packet.Name,
            packet.TemporaryName,
            packet.Locale,
            guildName);

        if (!GuildHelper.IsValidName(guildName))
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildNameNotValid"
            });

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var normalizedGuildName = guildName.ToLower();
        var sameNameGuilds = dbContext.Guilds
            .Where(x => x.Name.ToLower() == normalizedGuildName);

        // Older servers could leave the guild row behind after its last member quit.
        // Delete that empty row before checking the unique name so it can be reused.
        sameNameGuilds
            .Where(x => !x.Members.Any())
            .ExecuteDelete();

        var nameTaken = sameNameGuilds.Any();

        if (nameTaken)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildNameAlreadyExists"
            });

            return true;
        }

        var dbCharacter = dbContext.Characters
            .SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
            return true;

        var dbGuild = new DbGuild
        {
            Name = guildName
        };

        dbContext.Guilds.Add(dbGuild);

        var dbGuildMember = new DbGuildMember
        {
            Id = dbCharacter.Id,

            Role = GuildRole.Leader.Id,

            Guild = dbGuild
        };

        dbContext.GuildMembers.Add(dbGuildMember);

        dbCharacter.GuildMemberId = dbGuildMember.Id;

        var created = true;

        try
        {
            if (dbContext.SaveChanges() <= 0)
            {
                _logger.LogWarning("Failed to create guild \"{name}\".", guildName);
                created = false;
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to create guild \"{name}\".", guildName);
            created = false;
        }

        if (!created)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildNameAlreadyExists"
            });

            return true;
        }

        var guildCreateGuildPacket = new GuildCreateGuildPacket
        {
            CreateGuild = false
        };

        connection.SendTunneled(guildCreateGuildPacket);

        var guildData = new GuildData
        {
            Guid = dbGuild.Id,

            Name = dbGuild.Name,

            CanRenameGuild = true,

            MaxMembers = dbGuild.MaxMembers
        };

        var memberGuid = GuidHelper.GetPlayerGuid(dbGuildMember.Id);

        guildData.Members.Add(memberGuid, new GuildMember
        {
            Guid = memberGuid,

            Role = dbGuildMember.Role,

            Name = connection.Player.Name,

            Online = true,

            WorldId = connection.Player.Zone.Id,

            ProfileId = connection.Player.ActiveProfileId,
            ProfileRank = connection.Player.ActiveProfile.Rank,
        });

        connection.Player.GuildData = guildData;

        var guildDataFullPacket = new GuildDataFullPacket
        {
            Data = guildData,
            Guid = guildData.Guid
        };

        connection.SendTunneled(guildDataFullPacket);

        var guildPlayerStatusUpdatePacket = new GuildPlayerStatusUpdatePacket
        {
            PlayerGuid = connection.Player.Guid,
            GuildGuid = guildData.Guid,
            IsInGuild = true
        };

        connection.Player.SendTunneledToVisible(guildPlayerStatusUpdatePacket, true);

        connection.SendTunneled(new GuildErrorPacket
        {
            MessageName = "GuildCreationSuccess"
        });

        return true;
    }
}
