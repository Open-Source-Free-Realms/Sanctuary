using System;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Database;
using Sanctuary.Packet.Common;
using Sanctuary.Core.Configuration;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class CharacterLoginRequestHandler
{
    private static ILogger _logger = null!;
    private static GatewayServer _gatewayServer = null!;
    private static LoginServerOptions _options = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CharacterLoginRequestHandler));

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

        if (connection.Guid == 0)
        {
            characterLoginReply.Status = 6;

            connection.Send(characterLoginReply);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var character = dbContext.Characters.SingleOrDefault(x => x.UserGuid == connection.Guid && x.Guid == packet.EntityKey);

        if (character is null)
        {
            characterLoginReply.Status = 6;

            connection.Send(characterLoginReply);

            return true;
        }

        var ticket = Guid.NewGuid();

        character.Ticket = ticket;
        character.LastLogin = DateTimeOffset.UtcNow;

        if (dbContext.SaveChanges() <= 0)
        {
            characterLoginReply.Status = 6;

            connection.Send(characterLoginReply);
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
        if (gatewayServer.OnlineCharacters.Contains(character.Guid))
        {
            characterLoginReply.Status = 8;

            connection.Send(characterLoginReply);

            return true;
        }

        characterLoginReply.Status = 1;

        var clientCharacterData = new ClientCharacterData
        {
            ServerAddress = gatewayServer.ServerAddress,
            ServerTicket = ticket.ToString(),
            Guid = character.Guid
        };

        characterLoginReply.Payload = clientCharacterData.Serialize();

        connection.Send(characterLoginReply);

        return true;
    }
}