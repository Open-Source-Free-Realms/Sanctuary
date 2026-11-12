using System;
using System.Collections.Generic;
using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Core.IO;
using Sanctuary.Login.Handlers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Login;

public class GatewayConnection : LoginServerConnection
{
    private readonly ILogger _logger;
    private readonly GatewayServer _gatewayServer;

    public string ServerAddress { get; set; } = null!;
    public GameServerData ServerData { get; set; } = null!;

    // Online character id -> the gateway session that logged it in. A logout only counts for the session it names,
    // so a dropped session's late logout can't mark a newer login offline.
    // Only touched from the login server's single loop thread, so no lock.
    private readonly Dictionary<ulong, ulong> _onlineSessions = new();

    public int OnlineCount => _onlineSessions.Count;

    public bool IsOnline(ulong characterId) => _onlineSessions.ContainsKey(characterId);

    public void MarkOnline(ulong characterId, ulong session) => _onlineSessions[characterId] = session;

    public void MarkReleased(ulong characterId) => _onlineSessions.Remove(characterId);

    public bool TryLogout(ulong characterId, ulong session)
    {
        if (!_onlineSessions.TryGetValue(characterId, out var current) || current != session)
            return false;

        _onlineSessions.Remove(characterId);
        return true;
    }

    public GatewayConnection(ILogger<GatewayConnection> logger, IOptionsMonitor<LoginServerOptions> options, GatewayServer gatewayServer, SocketAddress socketAddress, int connectCode) : base(options, gatewayServer, socketAddress, connectCode)
    {
        _logger = logger;
        _gatewayServer = gatewayServer;
    }

    public override void OnTerminated()
    {
        base.OnTerminated();

        var reason = DisconnectReason == DisconnectReason.OtherSideTerminated
            ? OtherSideDisconnectReason
            : DisconnectReason;

        _logger.LogInformation("{connection} disconnected. {reason}", this, reason);
    }

    public override void OnRoutePacket(Span<byte> data)
    {
        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(data));
            return;
        }

        var handled = opCode switch
        {
            GatewayLoginRequest.OpCode => GatewayLoginRequestHandler.HandlePacket(this, data),
            GatewayCharacterLogin.OpCode => GatewayCharacterLoginHandler.HandlePacket(this, data),
            GatewayCharacterLogout.OpCode => GatewayCharacterLogoutHandler.HandlePacket(this, data),
            GatewayCharacterReleaseReply.OpCode => GatewayCharacterReleaseReplyHandler.HandlePacket(this, data),
            _ => false
        };

#if DEBUG
        if (!handled)
        {
            _logger.LogWarning("{connection} received an unhandled packet. ( OpCode: {opcode}, Data: {data} )", this, opCode, Convert.ToHexString(data));
        }
#endif
    }

    public void Send(ISerializablePacket packet)
    {
        var data = packet.Serialize();

        Send(UdpChannel.Reliable1, data);
    }
}