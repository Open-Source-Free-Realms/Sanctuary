using System;
using System.Diagnostics;

using Sanctuary.UdpLibrary.Configuration;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.UdpLibrary.Tests;

internal class TestManager : UdpManager<TestConnection>
{
    private readonly bool _server;

    public TestManager(bool server, UdpParams udpParams, IServiceProvider serviceProvider) : base(udpParams, serviceProvider)
    {
        _server = server;
    }

    public override bool OnConnectRequest(UdpConnection udpConnection)
    {
        Debug.WriteLine($"{udpConnection}", nameof(OnConnectRequest));

        Span<byte> buf = stackalloc byte[1];
        udpConnection.Send(UdpChannel.Reliable1, buf);

        return true;
    }
}