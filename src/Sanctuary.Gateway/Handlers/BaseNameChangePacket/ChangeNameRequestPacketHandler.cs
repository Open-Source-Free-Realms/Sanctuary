using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Gateway.Services;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ChangeNameRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static BanStore _banStore = null!;
    private static IpHistoryStore _ipHistoryStore = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ChangeNameRequestPacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _banStore = serviceProvider.GetRequiredService<BanStore>();
        _ipHistoryStore = serviceProvider.GetRequiredService<IpHistoryStore>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ChangeNameRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ChangeNameRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ChangeNameRequestPacket), packet);

        if (connection.Player.Guid != packet.Guid)
            _logger.LogError("Invalid player guid. {guid}", packet.Guid);

        if (packet.Type == NameChangeType.Guild)
            return OnChangeGuildName(connection, packet);

        var nameChangeResponsePacket = new NameChangeResponsePacket
        {
            Type = packet.Type,
            Guid = packet.Guid,
            Name = packet.Name
        };

        nameChangeResponsePacket.Result = packet.Type switch
        {
            NameChangeType.Character => OnChangeCharacterName(connection, packet),
            _ => ChangeNameResponse.Error
        };

        connection.SendTunneled(nameChangeResponsePacket);

        return true;
    }

    private static ChangeNameResponse OnChangeCharacterName(GatewayConnection connection, ChangeNameRequestPacket packet)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var dbCharacter = dbContext.Characters
            .Include(x => x.User)
            .FirstOrDefault(x => x.Id == characterId);

        if (dbCharacter is null)
            return ChangeNameResponse.Error;

        var oldName = $"{dbCharacter.FirstName} {dbCharacter.LastName}".Trim();
        var newName = $"{packet.Name.FirstName} {packet.Name.LastName}".Trim();

        dbCharacter.FirstName = packet.Name.FirstName;
        dbCharacter.LastName = packet.Name.LastName;
        dbCharacter.FullName = newName;

        if (dbContext.SaveChanges() <= 0)
            return ChangeNameResponse.Error;

        connection.Player.Name.FirstName = packet.Name.FirstName;
        connection.Player.Name.LastName = packet.Name.LastName;

        if (dbCharacter.UserId != 0)
        {
            try
            {
                _ipHistoryStore.UpdateCharacterNameForUser(dbCharacter.UserId, oldName, newName);
                _banStore.UpdateCharacterNameForUser(dbCharacter.UserId, oldName, newName);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to update json stores after rename. UserId: {userId}, CharacterId: {characterId}, OldName: {oldName}, NewName: {newName}",
                    dbCharacter.UserId,
                    dbCharacter.Id,
                    oldName,
                    newName);
            }
        }

        var playerUpdatePacketRenamePlayer = new PlayerUpdatePacketRenamePlayer
        {
            Guid = connection.Player.Guid,
            Name = connection.Player.Name
        };

        connection.Player.SendTunneledToVisible(playerUpdatePacketRenamePlayer, true);

        var friendRenamePacket = new FriendRenamePacket
        {
            Guid = connection.Player.Guid,
            Name = connection.Player.Name.FullName
        };

        foreach (var friend in connection.Player.Friends)
        {
            if (!_zoneManager.TryGetPlayer(friend.Guid, out var friendPlayer))
                continue;

            friendPlayer.SendTunneled(friendRenamePacket);
        }

        return ChangeNameResponse.Pending;
    }

    private static bool OnChangeGuildName(GatewayConnection connection, ChangeNameRequestPacket packet)
    {
        var guildName = NormalizeGuildName(packet.Name.FullName);

        if (connection.Player.GuildData is null)
        {
            SendGuildNameChangeError(connection, packet, ChangeNameResponse.Error);
            return true;
        }

        var guildGuid = connection.Player.GuildData.Guid;

        if (!IsValidGuildName(guildName))
        {
            SendGuildNameChangeError(connection, packet, ChangeNameResponse.Error);
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var role = dbContext.GuildMembers
            .AsNoTracking()
            .Where(x => x.GuildId == guildGuid && x.Id == characterId)
            .Select(x => (int?)x.Role)
            .SingleOrDefault();

        if (role != 1)
        {
            SendGuildNameChangeError(connection, packet, ChangeNameResponse.Error);
            return true;
        }

        var normalizedGuildName = guildName.ToLower();
        var nameTaken = dbContext.Guilds.Any(x => x.Id != guildGuid && x.Name.ToLower() == normalizedGuildName);

        if (nameTaken)
        {
            SendGuildNameChangeError(connection, packet, ChangeNameResponse.AlreadyInProgress);
            return true;
        }

        var updated = dbContext.Guilds
            .Where(x => x.Id == guildGuid)
            .ExecuteUpdate(x => x.SetProperty(g => g.Name, guildName));

        if (updated <= 0)
        {
            SendGuildNameChangeError(connection, packet, ChangeNameResponse.Error);
            return true;
        }

        connection.Player.GuildData.Name = guildName;

        var guildNameUpdatePacket = new GuildNameUpdatePacket
        {
            Guid = guildGuid,
            Name = guildName
        };

        var notifiedPlayers = 0;
        foreach (var player in _zoneManager.GetPlayers())
        {
            if (player.GuildData is not null && player.GuildData.Guid == guildGuid)
                player.GuildData.Name = guildName;

            player.SendTunneled(guildNameUpdatePacket);
            notifiedPlayers++;
        }

        _logger.LogTrace(
            "Guild name changed immediately. GuildGuid: {guildGuid}, CharacterId: {characterId}, Name: \"{name}\", NotifiedPlayers: {notifiedPlayers}",
            guildGuid,
            characterId,
            guildName,
            notifiedPlayers);

        return true;
    }

    private static void SendGuildNameChangeError(GatewayConnection connection, ChangeNameRequestPacket packet, ChangeNameResponse result)
    {
        connection.SendTunneled(new NameChangeResponsePacket
        {
            Type = packet.Type,
            Guid = packet.Guid,
            Name = packet.Name,
            Result = result
        });
    }

    private static string NormalizeGuildName(string? name)
    {
        return string.Join(' ', (name ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsValidGuildName(string name)
    {
        if (name.Length is < 3 or > 32)
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '\'' or '-');
    }
}
