using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Gateway.Services;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketLoginHandler
{
    private static ILogger _logger = null!;
    private static ILogger _serverEvents = null!;
    private static LoginClient _loginClient = null!;
    private static GatewayServerOptions _options = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static BanStore _banStore = null!;
    private static IpHistoryStore _ipHistoryStore = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketLoginHandler));
        _serverEvents = loggerFactory.CreateLogger("ServerEvents");

        _loginClient = serviceProvider.GetRequiredService<LoginClient>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _banStore = serviceProvider.GetRequiredService<BanStore>();
        _ipHistoryStore = serviceProvider.GetRequiredService<IpHistoryStore>();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<GatewayServerOptions>>();
        _options = options.CurrentValue;
        options.OnChange(o => _options = o);
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        try
        {

            if (connection.Player is not null)
            {
                _logger.LogWarning(
                    "{connection} attempted duplicate login.",
                    connection);

                connection.Disconnect();
                return true;
            }

            if (!PacketLogin.TryDeserialize(data, out var packet))
            {
                _logger.LogError("Failed to deserialize {packet}.", nameof(PacketLogin));
                return false;
            }

            _logger.LogInformation("Received {name} packet. ( Guid: {guid}, Version: \"{version}\" )", nameof(PacketLogin), packet.Guid, packet.Version);

            var packetLoginReply = new PacketLoginReply();

            if (packet.Version != _options.ClientVersion)
            {
                _logger.LogError(
                    "{connection} connected with a different client version. ( Guid: {guid}, ClientVersion: \"{version}\" )",
                    connection,
                    packet.Guid,
                    packet.Version);

                connection.Send(packetLoginReply);
                connection.Disconnect();
                return true;
            }

            if (!Guid.TryParse(packet.Ticket, out var ticket))
            {
                _logger.LogError(
                    "{connection} connected with an invalid ticket. ( Guid: {guid}, Ticket: \"{ticket}\" )",
                    connection,
                    packet.Guid,
                    packet.Ticket);

                connection.Send(packetLoginReply);
                connection.Disconnect();
                return true;
            }

           

            using var dbContext = _dbContextFactory.CreateDbContext();

            var character = dbContext.Characters
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Items)
                .Include(x => x.Titles)
                .Include(x => x.Mounts)
                .Include(x => x.Friends)
                    .ThenInclude(x => x.FriendCharacter)
                .Include(x => x.Ignores)
                    .ThenInclude(x => x.IgnoreCharacter)
                .Include(x => x.Profiles)
                    .ThenInclude(x => x.Items)
                .AsSplitQuery()
                .SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(packet.Guid) && x.Ticket == ticket);

            if (character is null)
            {
                _logger.LogWarning(
                    "{connection} connected with an invalid guid or ticket. ( Guid: {guid}, Ticket: \"{ticket}\" )",
                    connection,
                    packet.Guid,
                    packet.Ticket);

                connection.Send(packetLoginReply);
                connection.Disconnect();
                return true;
            }

            var characterName = $"{character.FirstName} {character.LastName}".Trim();
            var ipAddress = connection.EndPoint?.Address?.ToString() ?? "unknown";

            if (character.GuildMemberId is not null)
            {
                var guild = dbContext.Guilds
                    .AsNoTracking()
                    .Include(x => x.Members)
                        .ThenInclude(x => x.Character)
                    .AsSplitQuery()
                    .SingleOrDefault(x => x.Members.Any(m => m.Id == character.GuildMemberId));

                var guildMember = guild?.Members.SingleOrDefault(x => x.Id == character.GuildMemberId);

                if (guildMember is not null)
                {
                    guildMember.Guild = guild!;
                    character.GuildMember = guildMember;
                }
            }

            _logger.LogInformation(
                "Gateway login character loaded. UserId: {userId}, Username: {username}, CharacterId: {characterId}, CharacterName: \"{characterName}\", PlayerGuid: {playerGuid}, IP: {ip}, Items: {items}, Friends: {friends}, Ignores: {ignores}, Profiles: {profiles}, Mounts: {mounts}, Titles: {titles}, GuildMemberId: {guildMemberId}, GuildId: {guildId}, GuildMembers: {guildMembers}",
                character.User?.Id,
                character.User?.Username,
                character.Id,
                characterName,
                packet.Guid,
                ipAddress,
                character.Items.Count,
                character.Friends.Count,
                character.Ignores.Count,
                character.Profiles.Count,
                character.Mounts.Count,
                character.Titles.Count,
                character.GuildMemberId,
                character.GuildMember?.GuildId,
                character.GuildMember?.Guild?.Members.Count);

            try
            {
                connection.InitializeCipher(packet.Ticket);

                if (character.User is not null)
                {
                    var rawCharacterNames = dbContext.Characters
                        .AsNoTracking()
                        .Where(x => x.UserId == character.User.Id)
                        .Select(x => new
                        {
                            x.FirstName,
                            x.LastName
                        })
                        .ToList();

                    var characterNames = rawCharacterNames
                        .Select(x => $"{x.FirstName} {x.LastName}".Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    _logger.LogDebug(
                        "Checking login access. UserId: {userId}, Username: {username}, CharacterCount: {count}",
                        character.User.Id,
                        character.User.Username,
                        characterNames.Count);

                    if (_banStore.IsBanned(
                        character.User.Id,
                        character.User.Username,
                        characterNames,
                        ipAddress))
                    {
                        _logger.LogWarning(
                            "{connection} blocked login for banned user. ( UserId: {userId}, Username: \"{username}\", Character: \"{characterName}\" )",
                            connection,
                            character.User.Id,
                            character.User.Username,
                            $"{character.FirstName} {character.LastName}".Trim());

                        connection.Send(packetLoginReply);
                        connection.Disconnect();
                        return true;
                    }

                    _ipHistoryStore.RecordLogin(
                        character.User.Id,
                        character.User.Username,
                        characterNames,
                        ipAddress);

                    connection.UserId = character.User.Id;
                    connection.Username = character.User.Username;

                    _logger.LogDebug(
                        "Recorded login access history. UserId: {userId}, Username: {username}",
                        character.User.Id,
                        character.User.Username);
                }
                else
                {
                    _logger.LogWarning(
                        "{connection} login character had no attached user. CharacterId: {characterId}",
                        connection,
                        character.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login access processing failed for character {characterId}. Allowing login to continue.", character.Id);
            }

            if (!connection.CreatePlayerFromDatabase(character))
            {
                connection.Send(packetLoginReply);
                connection.Disconnect();
                return true;
            }

            _serverEvents.LogInformation(
                "User online. UserId: {userId}, Username: {username}, Character: \"{characterName}\", IP: {ip}",
                character.User?.Id,
                character.User?.Username ?? string.Empty,
                characterName,
                ipAddress);

            _loginClient.SendCharacterLogin(character.Id);

            packetLoginReply.Success = true;
            connection.Send(packetLoginReply);

            try
            {
                connection.SendInitializationParameters();
                connection.SendZoneDetails();
                connection.ClientGameSettings();
                connection.SendItemDefinitions();

                ItemActionBarService.AddOwnedCarouselAliasesToSelfInventory(connection, _resourceManager, _logger);
                try
                {
                    connection.SendSelfToClient();
                }
                finally
                {
                    ItemActionBarService.RemoveCarouselAliasesFromServerInventory(connection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Post-login initialization failed.");

                connection.Disconnect();
                return true;
            }

#if !DEBUG
            var result = dbContext.Characters
                .Where(x => x.Id == character.Id && x.Ticket == ticket)
                .ExecuteUpdate(x => x.SetProperty(x => x.Ticket, (Guid?)null));

            if (result <= 0)
            {
                _logger.LogWarning(
                    "{connection} failed to consume login ticket after initialization. CharacterId: {characterId}",
                    connection,
                    character.Id);

                connection.Disconnect();
                return true;
            }
#endif

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in PacketLoginHandler.");

            try
            {
                connection.Send(new PacketLoginReply());
            }
            catch
            {
            }

            connection.Disconnect();
            return true;
        }
    }

}
