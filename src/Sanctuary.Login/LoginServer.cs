using System;
using System.Buffers.Binary;
using System.Linq;
using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Configuration;

namespace Sanctuary.Login;

public class LoginServer : UdpManager<LoginConnection>
{
    private readonly ILogger _logger;
    private readonly GatewayServer _gatewayServer;

    private LoginServerOptions _options;

    public LoginServer(ILogger<LoginServer> logger, IOptionsMonitor<LoginServerOptions> options, GatewayServer gatewayServer, UdpParams udpParams, IServiceProvider serviceProvider) : base(udpParams, serviceProvider)
    {
        _logger = logger;
        _gatewayServer = gatewayServer;

        _options = options.CurrentValue;
        options.OnChange(o => _options = o);
    }

    public override bool OnConnectRequest(UdpConnection udpConnection)
    {
        _logger.LogInformation("{connection} connected.", udpConnection);

        return true;
    }

    public override void OnServerStatusRequest(SocketAddress socketAddress)
    {
        // 1 - Is Online
        // 1 - Is Locked
        // 4 - Online Players
        Span<byte> buf = stackalloc byte[20];

        buf[0] = 1;
        buf[1] = Convert.ToByte(_options.IsLocked);

        var onlinePlayers = _gatewayServer.Gateways.Sum(x => x.OnlineCharacters.Count);

        BinaryPrimitives.WriteInt32LittleEndian(buf.Slice(2), onlinePlayers);

        ActualSend(buf, buf.Length, socketAddress);
    }
}