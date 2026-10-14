using System;
using System.Net;

using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Abstractions;

namespace Sanctuary.Login;

public abstract class LoginServerConnection : UdpConnection
{
    private readonly IDisposable? _optionsSubscription;

    protected LoginServerOptions Options { get; private set; }

    protected LoginServerConnection(IOptionsMonitor<LoginServerOptions> options, IUdpManager udpManager, SocketAddress socketAddress, int connectCode) : base(udpManager, socketAddress, connectCode)
    {
        Options = options.CurrentValue;
        _optionsSubscription = options.OnChange(loginServerOptions => Options = loginServerOptions);
    }

    public override void OnTerminated()
    {
        _optionsSubscription?.Dispose();
    }
}
