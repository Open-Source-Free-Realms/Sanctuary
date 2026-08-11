using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketLoginHandler
{
    private static ILogger _logger = null!;
    private static LoginClient _loginClient = null!;
    private static GatewayServerOptions _options = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketLoginHandler));

        _loginClient = serviceProvider.GetRequiredService<LoginClient>();

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<GatewayServerOptions>>();
        _options = options.CurrentValue;
        options.OnChange(o => _options = o);
    }

    private static void AddRefereeToProfile(DbCharacter character, DatabaseContext dbContext)
    {
        const int refereeId = 58;
        if (!_resourceManager.Profiles.TryGetValue(refereeId, out var refereeProfileData))
        {
            _logger.LogWarning("Referee profile with ID {refereeId} does not exist in the resource manager.", refereeId);
            return;
        }

        // Check if the character already has the referee profile
        if (character.Profiles.Any(p => p.Id == refereeId))
        {
            _logger.LogInformation("Character {characterId} already has the referee profile.", character.Id);
            return;
        }

        DbProfile refereeProfile = new DbProfile
        {
            CharacterId = character.Id,
            Id = refereeId,
            Level = 20
        };

        var existingItemIds = character.Items.Select(x => x.Id).ToHashSet();

        ProfileHelper.GrantDefaultItems(character, refereeProfile, refereeProfileData, _resourceManager);
        dbContext.Attach(character);

        foreach (var item in character.Items)
        {
            if (!existingItemIds.Contains(item.Id))
                dbContext.Entry(item).State = EntityState.Added;
        }

        dbContext.Entry(refereeProfile).State = EntityState.Added;

        dbContext.SaveChanges();
        character.Profiles.Add(refereeProfile);
        return;
    }

    private static void RemoveRefereeFromProfile(DbCharacter character, DatabaseContext dbContext)
    {
        const int refereeId = 58;
        dbContext.Profiles.Where(p => p.CharacterId == character.Id && p.Id == refereeId).ExecuteDelete();
        dbContext.SaveChanges();
        var profileToRemove = character.Profiles.Where(p => p.CharacterId == character.Id && p.Id == refereeId)
        .FirstOrDefault(p => p.Id == refereeId);
        if (profileToRemove != null)
        {
            character.Profiles.Remove(profileToRemove);
        }
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketLogin.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketLogin));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketLogin), packet);

        var packetLoginReply = new PacketLoginReply();

        if (packet.Version != _options.ClientVersion)
        {
            _logger.LogError("{connection} connected with a different client version. ( Guid: {guid}, ClientVersion: \"{version}\" )", connection, packet.Guid, packet.Version);

            connection.Send(packetLoginReply);

            connection.Disconnect();

            return true;
        }

        if (!Guid.TryParse(packet.Ticket, out var ticket))
        {
            _logger.LogError("{connection} connected with an invalid ticket. ( Guid: {guid}, Ticket: \"{ticket}\" )", connection, packet.Guid, packet.Ticket);

            connection.Send(packetLoginReply);

            connection.Disconnect();

            return true;
        }

        // Use ticket as key.
        connection.InitializeCipher(packet.Ticket);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var character = dbContext.Characters
            .AsNoTrackingWithIdentityResolution()
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
            .Include(x => x.GuildMember!)
                .ThenInclude(x => x.Guild)
                    .ThenInclude(x => x.Members)
                        .ThenInclude(x => x.Character)
            .AsSplitQuery()
            .SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(packet.Guid)
#if !DEBUG
                && x.Ticket == ticket
#endif
            );

        if (character is null)
        {
            _logger.LogWarning("{connection} connected with an invalid guid or ticket. ( Guid: {guid}, Ticket: \"{ticket}\" )", connection, packet.Guid, packet.Ticket);

            connection.Send(packetLoginReply);

            connection.Disconnect();

            return true;
        }

        if (character.User.LockedUntil != null)
        {
            DateTimeOffset currentTime = DateTimeOffset.UtcNow;
            DateTimeOffset? lockedUntil = character.User.LockedUntil;
            if (lockedUntil <= currentTime)
            {
                dbContext.Users
                    .Where(x => x.Id == character.User.Id)
                    .ExecuteUpdate(x => x
                        .SetProperty(u => u.LockedUntil, (DateTimeOffset?)null));
            }
            else
            {
                _logger.LogWarning("{connection} connected with a banned account. ( Guid: {guid}, Ticket: \"{ticket}\" )", connection, packet.Guid, packet.Ticket);

                connection.Send(packetLoginReply);

                connection.Disconnect();

                return true;
            }
        }
      
        var orphanedIgnores = character.Ignores
            .Where(x => x.IgnoreCharacter is null)
            .ToList();

        if (orphanedIgnores.Count > 0)
        {
            var orphanedIgnoreIds = orphanedIgnores
                .Select(x => x.IgnoreCharacterId)
                .ToList();

            dbContext.Ignores
                .Where(x => x.CharacterId == character.Id && orphanedIgnoreIds.Contains(x.IgnoreCharacterId))
                .ExecuteDelete();

            foreach (var orphanedIgnore in orphanedIgnores)
            {
                character.Ignores.Remove(orphanedIgnore);
            }
        }

        
        if (character.User.IsMod || character.User.IsAdmin)
        {
            AddRefereeToProfile(character, dbContext);
        }
        else
        {
            // if user is no longer a mod, remove referee profile
            RemoveRefereeFromProfile(character, dbContext);
        }
#if !DEBUG
        var result = dbContext.Characters
            .Where(x => x.Id == character.Id)
            .ExecuteUpdate(x => x.SetProperty(x => x.Ticket, (Guid?)null));

        if (result <= 0)
        {
            connection.Send(packetLoginReply);

            connection.Disconnect();

            return true;
        }
#endif

        if (!connection.CreatePlayerFromDatabase(character))
        {
            connection.Send(packetLoginReply);

            connection.Disconnect();

            return true;
        }

        _loginClient.SendCharacterLogin(character.Id);

        packetLoginReply.Success = true;

        connection.Send(packetLoginReply);

        // TODO
        // AchievementObjectiveActivatedPacket - Part 1?
        // EncounterOverworldCombatPacket

        connection.SendInitializationParameters();
        connection.SendZoneDetails();
        connection.ClientGameSettings();
        connection.SendItemDefinitions();

        // TODO
        // AnnoucementDataSendPacket
        // AchievementObjectiveActivatedPacket - Part 2?

        connection.SendSelfToClient();

        _logger.LogInformation("{address} successfully logged in with character {name} ({id}).", connection.EndPoint.Address, character.FullName, character.Id);

        return true;
    }
}
