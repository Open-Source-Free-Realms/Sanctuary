using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class CharacterLoginRequestHandler
{
    private static ILogger _logger = null!;
    private static ILogger _serverEvents = null!;
    private static GatewayServer _gatewayServer = null!;
    private static LoginServerOptions _options = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CharacterLoginRequestHandler));
        _serverEvents = loggerFactory.CreateLogger("ServerEvents");

        _gatewayServer = serviceProvider.GetRequiredService<GatewayServer>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<LoginServerOptions>>();
        _options = options.CurrentValue;
        options.OnChange(o => _options = o);
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!CharacterLoginRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CharacterLoginRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CharacterLoginRequest), packet);

        var characterLoginReply = new CharacterLoginReply();

        if (!ClientLoginData.TryDeserialize(packet.Payload, out var clientLoginData))
        {
            connection.Send(characterLoginReply);

            return true;
        }

        if (connection.UserId == 0)
        {
            characterLoginReply.Status = 6;

            connection.Send(characterLoginReply);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var character = dbContext.Characters.SingleOrDefault(x => x.UserId == connection.UserId && x.Id == GuidHelper.GetPlayerId(packet.EntityKey));

        if (character is null)
        {
            characterLoginReply.Status = 6;

            connection.Send(characterLoginReply);

            return true;
        }

        Guid ticket;

        try
        {
            ticket = Guid.NewGuid();

            character.Ticket = ticket;
            character.LastLogin = DateTimeOffset.UtcNow;

            dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save character login ticket to database. ( UserId: {userId}, CharacterId: {characterId} )",
                connection.UserId,
                character.Id);

            characterLoginReply.Status = 6;
            connection.Send(characterLoginReply);

            return true;
        }

        // TODO: Client currently doesn't let the user pick a server so default to the first one.

        var gatewayServer = _gatewayServer.Gateways.FirstOrDefault();

        if (gatewayServer is null)
        {
            characterLoginReply.Status = 7;

            connection.Send(characterLoginReply);

            return true;
        }

        // Character is already logged in.
        if (gatewayServer.OnlineCharacters.Contains(character.Id))
        {
            characterLoginReply.Status = 8;

            connection.Send(characterLoginReply);

            return true;
        }

        characterLoginReply.Status = 1;

        var serverTicket = ticket.ToString("N");

        var clientCharacterData = new ClientCharacterData
        {
            ServerAddress = gatewayServer.ServerAddress,
            ServerTicket = serverTicket,
            CryptoKey = serverTicket, // Use ticket as key.
            Guid = GuidHelper.GetPlayerGuid(character.Id)
        };

        characterLoginReply.Payload = clientCharacterData.Serialize();

        connection.Send(characterLoginReply);

        var characterName = $"{character.FirstName} {character.LastName}".Trim();

        _serverEvents.LogInformation(
            "Player selected character. UserId: {userId}, Character: \"{characterName}\", IP: {ip}, Gateway: {gateway}",
            connection.UserId,
            characterName,
            connection.EndPoint.Address,
            gatewayServer.ServerAddress);

        return true;
    }
}
