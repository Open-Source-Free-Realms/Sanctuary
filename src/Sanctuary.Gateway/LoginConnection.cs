using System;
using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Core.IO;
using Sanctuary.Gateway.Handlers;
using Sanctuary.Packet;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Gateway;

public class LoginConnection : UdpConnection
{
    private readonly ILogger _logger;
    private readonly GatewayServerOptions _options;

    public LoginConnection(ILogger<LoginConnection> logger, IOptions<GatewayServerOptions> options, LoginClient loginClient, SocketAddress socketAddress, long timeout) : base(loginClient, socketAddress, timeout)
    {
        _logger = logger;
        _options = options.Value;
    }

    public override void OnConnectComplete()
    {
        _logger.LogInformation("{connection} connected.", this);

        var gatewayLoginRequest = new GatewayLoginRequest();

        gatewayLoginRequest.Challenge = _options.LoginGatewayChallenge;

        // TODO: gatewayLoginRequest.Data

        gatewayLoginRequest.ServerAddress = _options.ServerAddress;

        Send(gatewayLoginRequest);
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
            GatewayLoginReply.OpCode => GatewayLoginReplyHandler.HandlePacket(this, data),
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