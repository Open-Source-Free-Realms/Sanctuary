using System;
using System.Net;

namespace Sanctuary.UdpLibrary.Abstractions;

public interface IUdpDriver
{
    bool SocketOpen(int port, int incomingBufferSize, int outgoingBufferSize, string? bindIpAddress);
    void SocketClose();

    int SocketReceive(Span<byte> buffer, SocketAddress socketAddress);
    bool SocketSend(ReadOnlySpan<byte> data, SocketAddress socketAddress);

    void SocketSendPortAlive(ReadOnlySpan<byte> data, SocketAddress socketAddress);

    bool SocketGetLocalIp(out IPAddress ipAddress);
    int SocketGetLocalPort();

    bool GetHostByName(out IPAddress ipAddress, string hostName);

    UdpClockStamp Clock();
    void Sleep(int lingerDelay);
}