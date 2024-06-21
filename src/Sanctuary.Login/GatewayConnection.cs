using System;
using System.Net;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Packet;
using Sanctuary.Core.IO;
using Sanctuary.UdpLibrary;
using Sanctuary.Packet.Common;
using Sanctuary.Login.Handlers;
using Sanctuary.Core.Configuration;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Login;

public class GatewayConnection : UdpConnection
{
    private readonly ILogger _logger;
    private readonly GatewayServer _gatewayServer;

    private LoginServerOptions _options;

    public string ServerAddress { get; set; } = null!;
    public GameServerData ServerData { get; set; } = null!;

    public HashSet<ulong> OnlineCharacters { get; set; } = new();

    public GatewayConnection(ILogger<GatewayConnection> logger, IOptionsMonitor<LoginServerOptions> options, GatewayServer gatewayServer, SocketAddress socketAddress, int connectCode) : base(gatewayServer, socketAddress, connectCode)
    {
        _logger = logger;
        _gatewayServer = gatewayServer;

        _options = options.CurrentValue;
        options.OnChange(o => _options = o);
    }

    public override void OnTerminated()
    {
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
            _ => false
        };

        if (!handled)
        {
            _logger.LogWarning("{connection} received an unhandled packet. ( OpCode: {opcode}, Data: {data} )", this, opCode, Convert.ToHexString(data));
        }
    }

    public void Send(ISerializablePacket packet)
    {
        var data = packet.Serialize();

        Send(UdpChannel.Reliable1, data);
    }
}