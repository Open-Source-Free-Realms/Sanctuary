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
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class CharacterLoginRequestHandler
{
    // The client sends a packet about every second, so five seconds of silence is a crashed session.
    private const uint CrashIdleMilliseconds = 5000;

    // How often to ask the gateway again while a login is waiting on the old session.
    private const long ReleaseRetryMilliseconds = 2000;

    // After a waiting login is let in, other waiters for that character hold off this long for it to reach the gateway.
    private const long HandoffMilliseconds = 10000;

    private static ILogger _logger = null!;
    private static GatewayServer _gatewayServer = null!;
    private static LoginServerOptions _options = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static uint _nextReleaseRequestId = 1;

    // The client only leaves its waiting state on status 1; any other status leaves it stuck at character select
    // until it is restarted. So a login for a character that's still online is held here, never refused.
    private static readonly List<PendingCharacterLogin> _pendingLogins = new();

    // Character id -> until when a just-admitted login has the character to itself.
    private static readonly Dictionary<ulong, long> _handoffs = new();

    private sealed class PendingCharacterLogin
    {
        public required LoginConnection Connection { get; init; }
        public required GatewayConnection Gateway { get; set; }
        public required ulong CharacterId { get; init; }
        public long LastRequestAt { get; set; } = long.MinValue / 2;
        public uint RequestId { get; set; }
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

        // TODO: Client currently doesn't let the user pick a server so default to the first one.

        var gatewayServer = GetGateway();

        if (gatewayServer is null)
        {
            characterLoginReply.Status = 7;

            connection.Send(characterLoginReply);

            return true;
        }

        // Still online: hold the login until the old session is released (gone quiet, a crash) or logs out on its own.
        if (IsBusy(gatewayServer, character.Id))
        {
            _logger.LogInformation("Character {id} is still online; holding the login until the old session is released or logs out.", character.Id);

            _pendingLogins.Add(new PendingCharacterLogin
            {
                Connection = connection,
                Gateway = gatewayServer,
                CharacterId = character.Id
            });

            ProcessPendingLogins();

            return true;
        }

        Admit(connection, gatewayServer, character.Id);

        return true;
    }

    public static void CompleteRelease(uint requestId, bool released)
    {
        var pending = _pendingLogins.Find(x => x.RequestId == requestId);

        if (pending is null)
            return;

        pending.RequestId = 0;

        _logger.LogDebug("Character {id} release: released={released}.", pending.CharacterId, released);

        // Normally the old session's logout has already cleared it; this covers the reply arriving first.
        if (released)
            pending.Gateway.MarkReleased(pending.CharacterId);

        ProcessPendingLogins();
    }

    /// <summary>
    /// Lets waiting logins in once their character is free, and keeps asking the gateway to release it meanwhile.
    /// Called every loop from <see cref="LoginService"/>.
    /// </summary>
    public static void ProcessPendingLogins()
    {
        if (_pendingLogins.Count == 0)
            return;

        var now = Environment.TickCount64;

        foreach (var pending in _pendingLogins.ToList())
        {
            if (pending.Connection.Status == Status.Disconnected)
            {
                _pendingLogins.Remove(pending);
                _logger.LogInformation("Stopped holding the login for character {id}; the client disconnected.", pending.CharacterId);
                continue;
            }

            // The gateway this login was waiting on went away (restart); follow the current one.
            if (pending.Gateway.Status == Status.Disconnected && GetGateway() is { } current)
                pending.Gateway = current;

            if (!IsBusy(pending.Gateway, pending.CharacterId))
            {
                _pendingLogins.Remove(pending);
                _logger.LogInformation("Character {id} is free; letting the held login in.", pending.CharacterId);
                Admit(pending.Connection, pending.Gateway, pending.CharacterId);
                continue;
            }

            // Ask again every so often; an unanswered request just gets retried.
            if (pending.Gateway.IsOnline(pending.CharacterId) && now - pending.LastRequestAt >= ReleaseRetryMilliseconds)
            {
                pending.RequestId = _nextReleaseRequestId++;
                pending.LastRequestAt = now;

                pending.Gateway.Send(new GatewayCharacterReleaseRequest
                {
                    RequestId = pending.RequestId,
                    Id = pending.CharacterId,
                    IdleThresholdMilliseconds = CrashIdleMilliseconds
                });
            }
        }
    }

    // Only gateways that completed their login (ServerAddress set); a dropped one may still be in the list briefly.
    private static GatewayConnection? GetGateway() => _gatewayServer.Gateways.LastOrDefault(x => !string.IsNullOrEmpty(x.ServerAddress));

    private static bool IsBusy(GatewayConnection gateway, ulong characterId)
    {
        if (gateway.IsOnline(characterId))
            return true;

        if (!_handoffs.TryGetValue(characterId, out var until))
            return false;

        if (Environment.TickCount64 < until)
            return true;

        _handoffs.Remove(characterId);
        return false;
    }

    // Issues a fresh ticket and sends status 1. The ticket is made here, not when the request arrived, so a login that
    // waited (or a second one for the same character) always carries the ticket the gateway will check.
    private static void Admit(LoginConnection connection, GatewayConnection gateway, ulong characterId)
    {
        var ticket = Guid.NewGuid();

        using var dbContext = _dbContextFactory.CreateDbContext();

        var updated = dbContext.Characters
            .Where(x => x.Id == characterId)
            .ExecuteUpdate(x => x.SetProperty(c => c.Ticket, (Guid?)ticket));

        if (updated <= 0)
        {
            connection.Send(new CharacterLoginReply { Status = 6 });
            return;
        }

        _handoffs[characterId] = Environment.TickCount64 + HandoffMilliseconds;

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
