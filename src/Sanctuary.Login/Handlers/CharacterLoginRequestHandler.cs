using System;
using System.Collections.Generic;
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
    private const long CrashIdleMilliseconds = 5000;

    private static ILogger _logger = null!;
    private static GatewayServer _gatewayServer = null!;
    private static LoginServerOptions _options = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static uint _nextIdleRequestId = 1;
    private static readonly Dictionary<uint, PendingCharacterLogin> _pendingLogins = new();

    private sealed class PendingCharacterLogin
    {
        public required LoginConnection Connection { get; init; }
        public required GatewayConnection Gateway { get; init; }
        public required Guid Ticket { get; init; }
        public required ulong CharacterId { get; init; }
    }

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

        var ticket = Guid.NewGuid();

        character.Ticket = ticket;

        if (dbContext.SaveChanges() <= 0)
        {
            characterLoginReply.Status = 6;

            connection.Send(characterLoginReply);

            return true;
        }

        // TODO: Client currently doesn't let the user pick a server so default to the first one.

        // Only gateways that completed their login (ServerAddress set); a dropped one may still be in the list briefly.
        var gatewayServer = _gatewayServer.Gateways.LastOrDefault(x => !string.IsNullOrEmpty(x.ServerAddress));

        if (gatewayServer is null)
        {
            characterLoginReply.Status = 7;

            connection.Send(characterLoginReply);

            return true;
        }

        // The client sends a packet about every second. Five seconds of silence is a crashed session.
        // Ask the gateway before answering, because status 8 closes the client and a live player must stay refused.
        if (gatewayServer.OnlineCharacters.Contains(character.Id))
        {
            var requestId = _nextIdleRequestId++;

            _pendingLogins[requestId] = new PendingCharacterLogin
            {
                Connection = connection,
                Gateway = gatewayServer,
                Ticket = ticket,
                CharacterId = character.Id
            };

            gatewayServer.Send(new GatewayCharacterIdleRequest
            {
                RequestId = requestId,
                Id = character.Id
            });

            return true;
        }

        SendLoginSuccess(connection, gatewayServer, ticket, character.Id);

        return true;
    }

    public static void CompleteIdleCheck(uint requestId, bool found, long idleMilliseconds)
    {
        if (!_pendingLogins.Remove(requestId, out var pending))
            return;

        if (pending.Connection.Status == UdpLibrary.Enumerations.Status.Disconnected)
            return;

        var crashed = !found || idleMilliseconds >= CrashIdleMilliseconds;

        _logger.LogInformation(
            "Character {id} idle check: found={found}, idle={idle} ms, crashed={crashed}.",
            pending.CharacterId, found, idleMilliseconds, crashed);

        if (!crashed)
        {
            pending.Connection.Send(new CharacterLoginReply { Status = 8 });
            return;
        }

        pending.Gateway.TryReleaseForNewLogin(pending.CharacterId);
        pending.Gateway.Send(new GatewayCharacterKick { Id = pending.CharacterId });
        SendLoginSuccess(pending.Connection, pending.Gateway, pending.Ticket, pending.CharacterId);
    }

    private static void SendLoginSuccess(LoginConnection connection, GatewayConnection gateway, Guid ticket, ulong characterId)
    {
        var serverTicket = ticket.ToString("N");

        var characterLoginReply = new CharacterLoginReply
        {
            Status = 1,
            Payload = new ClientCharacterData
            {
                ServerAddress = gateway.ServerAddress,
                ServerTicket = serverTicket,
                CryptoKey = serverTicket, // Use ticket as key.
                Guid = GuidHelper.GetPlayerGuid(characterId)
            }.Serialize()
        };

        connection.Send(characterLoginReply);
    }
}