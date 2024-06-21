using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Configuration;

namespace Sanctuary.Gateway;

public class LoginClient : UdpManager<LoginConnection>
{
    private readonly ILogger _logger;

    public IReadOnlyCollection<LoginConnection> Connections => ConnectionList;

    public LoginClient(ILogger<LoginClient> logger, UdpParams udpParams, IServiceProvider serviceProvider) : base(udpParams, serviceProvider)
    {
        _logger = logger;
    }

    public void SendCharacterLogin(ulong guid)
    {
        foreach (var connection in Connections)
        {
            var gatewayCharacterLogin = new GatewayCharacterLogin()
            {
                Guid = guid
            };

            connection.Send(gatewayCharacterLogin);
        }
    }

    public void SendCharacterLogout(ulong guid)
    {
        foreach (var connection in Connections)
        {
            var gatewayCharacterLogout = new GatewayCharacterLogout()
            {
                Guid = guid
            };

            connection.Send(gatewayCharacterLogout);
        }
    }
}