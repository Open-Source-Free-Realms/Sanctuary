using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Sanctuary.UdpLibrary.Abstractions;

namespace Sanctuary.UdpLibrary.Internal;

internal abstract class UdpPlatformDriver : IUdpDriver
{
    protected Socket? _socket;
    protected short _startTtl;

    public bool SocketOpen(int port, int incomingBufferSize, int outgoingBufferSize, string? bindIpAddress)
    {
        if (_socket is not null)
            return false;

        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            _socket.Blocking = false;

            _socket.SendBufferSize = outgoingBufferSize;
            _socket.ReceiveBufferSize = incomingBufferSize;

            _startTtl = _socket.Ttl;

            var ipAddress = IPAddress.Any;

            if (!string.IsNullOrWhiteSpace(bindIpAddress))
                ipAddress = IPAddress.Parse(bindIpAddress);

            _socket.Bind(new IPEndPoint(ipAddress, port));
        }
        catch
        {
            return false;
        }

        return true;
    }

    public void SocketClose()
    {
        _socket?.Close();
    }

    public abstract int SocketReceive(Span<byte> buffer, SocketAddress socketAddress);
    public abstract bool SocketSend(ReadOnlySpan<byte> data, SocketAddress socketAddress);

    public void SocketSendPortAlive(ReadOnlySpan<byte> data, SocketAddress socketAddress)
    {
        if (_socket is null)
            return;

        _socket.Ttl = 5;

        SocketSend(data, socketAddress);

        _socket.Ttl = _startTtl;
    }

    public bool SocketGetLocalIp(out IPAddress ipAddress)
    {
        if (_socket?.LocalEndPoint is not IPEndPoint endPoint)
        {
            ipAddress = IPAddress.None;
            return false;
        }

        ipAddress = endPoint.Address;

        return true;
    }

    public int SocketGetLocalPort()
    {
        if (_socket?.LocalEndPoint is not IPEndPoint endPoint)
            return 0;

        return endPoint.Port;
    }

    public bool GetHostByName(out IPAddress ipAddress, string hostName)
    {
        if (_socket is not null)
        {
            try
            {
                if (IPAddress.TryParse(hostName, out var address))
                {
                    ipAddress = address;

                    return true;
                }

                var ipHostEntry = Dns.GetHostEntry(hostName);

                if (ipHostEntry is not null)
                {
                    for (var i = 0; i < ipHostEntry.AddressList.Length; i++)
                    {
                        if (ipHostEntry.AddressList[i].AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        ipAddress = ipHostEntry.AddressList[i];

                        return true;
                    }
                }
            }
            catch
            {
            }
        }

        ipAddress = IPAddress.None;

        return false;
    }

    public void Sleep(int milliseconds)
    {
        Thread.Sleep(milliseconds);
    }

    public UdpClockStamp Clock()
    {
        return Environment.TickCount64;
    }
}