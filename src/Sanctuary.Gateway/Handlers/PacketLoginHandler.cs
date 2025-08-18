using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Database;
using Sanctuary.Game;
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

        using var dbContext = _dbContextFactory.CreateDbContext();

        var character = dbContext.Characters
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Titles)
            .Include(x => x.Mounts)
            .Include(x => x.Profiles)
                .ThenInclude(x => x.Items)
            .AsSplitQuery()
            .SingleOrDefault(x => x.Guid == packet.Guid && x.Ticket == ticket);

        if (character is null)
        {
            _logger.LogWarning("{connection} connected with an invalid guid or ticket. ( Guid: {guid}, Ticket: \"{ticket}\" )", connection, packet.Guid, packet.Ticket);

            connection.Send(packetLoginReply);

            connection.Disconnect();

            return true;
        }

#if !DEBUG
        var result = dbContext.Characters
            .Where(x => x.Guid == character.Guid)
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

        _loginClient.SendCharacterLogin(character.Guid);

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

        return true;
    }
}