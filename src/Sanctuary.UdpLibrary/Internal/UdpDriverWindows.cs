using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Sanctuary.UdpLibrary.Internal;

internal partial class UdpDriverWindows : UdpPlatformDriver
{
    [LibraryImport("ws2_32", SetLastError = true)]
    internal static unsafe partial int recvfrom(
        SafeSocketHandle socketHandle,
        Span<byte> pinnedBuffer,
        int len,
        SocketFlags socketFlags,
        Span<byte> socketAddress,
        ref int socketAddressSize);

    [LibraryImport("ws2_32", SetLastError = true)]
    internal static unsafe partial int sendto(
       SafeSocketHandle socketHandle,
       byte* pinnedBuffer,
       int len,
       SocketFlags socketFlags,
       ReadOnlySpan<byte> socketAddress,
       int socketAddressSize);

    public UdpDriverWindows()
    {
        _startTtl = 32;
    }

    public override int SocketReceive(Span<byte> buffer, SocketAddress socketAddress)
    {
        if (_socket is null)
            return -1;

        var addressLength = socketAddress.Buffer.Length;

        var bytesReceived = recvfrom(_socket.SafeHandle, buffer, buffer.Length, SocketFlags.None, socketAddress.Buffer.Span, ref addressLength);

        if (bytesReceived == (int)SocketError.SocketError)
        {
            var socketError = GetLastSocketError();

            if (socketError == SocketError.ConnectionReset)
                return 0;

            return -1;
        }

        return bytesReceived;
    }

    public unsafe override bool SocketSend(ReadOnlySpan<byte> data, SocketAddress socketAddress)
    {
        if (_socket is null)
            return false;

        int bytesSent;

        fixed (byte* bufferPtr = &MemoryMarshal.GetReference(data))
        {
            bytesSent = sendto(_socket.SafeHandle, bufferPtr, data.Length, SocketFlags.None, socketAddress.Buffer.Span, socketAddress.Size);
        }

        return bytesSent != (int)SocketError.SocketError;
    }

    public static SocketError GetLastSocketError()
    {
        var error = Marshal.GetLastWin32Error();

        return (SocketError)error;
    }
}