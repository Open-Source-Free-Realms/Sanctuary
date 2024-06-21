using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Configuration;

namespace Sanctuary.Login;

public class GatewayServer : UdpManager<GatewayConnection>
{
    private readonly ILogger _logger;

    public IReadOnlyCollection<GatewayConnection> Gateways => ConnectionList;

    public GatewayServer(ILogger<GatewayServer> logger, UdpParams udpParams, IServiceProvider serviceProvider) : base(udpParams, serviceProvider)
    {
        _logger = logger;
    }

    public override bool OnConnectRequest(UdpConnection udpConnection)
    {
        _logger.LogInformation("{connection} connected.", udpConnection);

        return true;
    }
}