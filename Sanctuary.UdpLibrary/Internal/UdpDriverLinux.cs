using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Sanctuary.UdpLibrary.Internal;

internal partial class UdpDriverLinux : UdpPlatformDriver
{
    [LibraryImport("libc", SetLastError = true)]
    internal static unsafe partial int recvfrom(
        SafeSocketHandle socketHandle,
        Span<byte> pinnedBuffer,
        int len,
        SocketFlags socketFlags,
        Span<byte> socketAddress,
        ref int socketAddressSize);

    [LibraryImport("libc", SetLastError = true)]
    internal static unsafe partial int sendto(
       SafeSocketHandle socketHandle,
       byte* pinnedBuffer,
       int len,
       SocketFlags socketFlags,
       ReadOnlySpan<byte> socketAddress,
       int socketAddressSize);

    public UdpDriverLinux()
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

        return error switch
        {
            0 /* SUCCESS */ => SocketError.Success,

            0x10002 /* EACCES */ => SocketError.AccessDenied,
            0x10003 /* EADDRINUSE */ => SocketError.AddressAlreadyInUse,
            0x10004 /* EADDRNOTAVAIL */ => SocketError.AddressNotAvailable,
            0x10005 /* EAFNOSUPPORT */ => SocketError.AddressFamilyNotSupported,
            0x10006 /* EAGAIN */ => SocketError.WouldBlock,
            0x10007 /* EALREADY */ => SocketError.AlreadyInProgress,
            0x10008 /* EBADF */ => SocketError.OperationAborted,

            0x1000B /* ECANCELED */ => SocketError.OperationAborted,

            0x1000D /* ECONNABORTED */ => SocketError.ConnectionAborted,
            0x1000E /* ECONNREFUSED */ => SocketError.ConnectionRefused,
            0x1000F /* ECONNRESET */ => SocketError.ConnectionReset,

            0x10011 /* EDESTADDRREQ */ => SocketError.DestinationAddressRequired,

            0x10015 /* EFAULT */ => SocketError.Fault,

            0x10017 /* EHOSTUNREACH */ => SocketError.HostUnreachable,

            0x1001A /* EINPROGRESS */ => SocketError.InProgress,
            0x1001B /* EINTR */ => SocketError.Interrupted,
            0x1001C /* EINVAL */ => SocketError.InvalidArgument,

            0x1001E /* EISCONN */ => SocketError.IsConnected,

            0x10021 /* EMFILE */ => SocketError.TooManyOpenSockets,

            0x10023 /* EMSGSIZE */ => SocketError.MessageSize,

            0x10026 /* ENETDOWN */ => SocketError.NetworkDown,
            0x10027 /* ENETRESET */ => SocketError.NetworkReset,
            0x10028 /* ENETUNREACH */ => SocketError.NetworkUnreachable,
            0x10029 /* ENFILE */ => SocketError.TooManyOpenSockets,
            0x1002A /* ENOBUFS */ => SocketError.NoBufferSpaceAvailable,

            0x1002D /* ENOENT */ => SocketError.AddressNotAvailable,

            0x10033 /* ENOPROTOOPT */ => SocketError.ProtocolOption,

            0x10038 /* ENOTCONN */ => SocketError.NotConnected,

            0x1003C /* ENOTSOCK */ => SocketError.NotSocket,
            0x1003D /* ENOTSUP */ => SocketError.OperationNotSupported,
            0x1003F /* ENXIO */ => SocketError.HostNotFound, // not perfect => but closest match available

            0x10042 /* EPERM */ => SocketError.AccessDenied,
            0x10043 /* EPIPE */ => SocketError.Shutdown,

            0x10045 /* EPROTONOSUPPORT */ => SocketError.ProtocolNotSupported,
            0x10046 /* EPROTOTYPE */ => SocketError.ProtocolType,

            0x1004D /* ETIMEDOUT */ => SocketError.TimedOut,

            0x1005E /* ESOCKTNOSUPPORT */ => SocketError.SocketNotSupported,

            0x10060 /* EPFNOSUPPORT */ => SocketError.ProtocolFamilyNotSupported,
            0x1006C /* ESHUTDOWN */ => SocketError.Disconnecting,

            0x10070 /* EHOSTDOWN */ => SocketError.HostDown,
            0x10071 /* ENODATA */ => SocketError.NoData,

            _ => SocketError.SocketError
        };
    }
}