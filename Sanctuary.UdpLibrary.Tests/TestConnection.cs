using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.IO.Compression;

using Sanctuary.Core.IO;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.UdpLibrary.Tests;

internal class TestConnection : UdpConnection
{
    private ulong _count;
    private readonly Random _random = new Random();

    public bool Server { get; set; }

    public TestConnection(UdpManager<TestConnection> udpManager, SocketAddress socketAddress, long timeout) : base(udpManager, socketAddress, timeout)
    {
    }

    public TestConnection(UdpManager<TestConnection> udpManager, SocketAddress socketAddress, int connectCode) : base(udpManager, socketAddress, connectCode)
    {
    }

    public override void OnConnectComplete()
    {
        Debug.WriteLine(nameof(OnConnectComplete));
    }

    public override void OnCrcReject(Span<byte> data)
    {
        Debug.WriteLine(Convert.ToHexString(data), nameof(OnCrcReject));
    }

    public override void OnPacketCorrupt(Span<byte> data, UdpCorruptionReason reason)
    {
        Debug.WriteLine($"Reason: {reason}\tData: {Convert.ToHexString(data)}", nameof(OnPacketCorrupt));
    }

    public override void OnRoutePacket(Span<byte> data)
    {
        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode))
            return;

        Span<byte> buf = stackalloc byte[256];

        _random.NextBytes(buf);

        switch (opCode)
        {
            // Client < Server
            case 1:
                buf[0] = 0x02;
                break;

            // Client > Server
            case 2:
                buf[0] = 0x01;
                break;

            default:
                throw new NotImplementedException();
        }

        Send(UdpChannel.Reliable1, buf);

        Debug.WriteLine(_count++, nameof(OnRoutePacket));
    }

    public override void OnTerminated()
    {
        Debug.WriteLine(DisconnectReason, nameof(OnTerminated));
    }

    #region Packet Compression

    protected override unsafe int DecryptUserSupplied(Span<byte> destData, Span<byte> sourceData)
    {
        if (sourceData[0] == 1)
        {
            // I don't like the fact we have to use unsafe code here but I couldn't
            // find another way to create a stream without copying the data.
            fixed (byte* pDestData = &destData[0])
            {
                fixed (byte* pSourceData = &sourceData[1])
                {
                    using var destStream = new UnmanagedMemoryStream(pDestData, destData.Length);
                    using var sourceStream = new UnmanagedMemoryStream(pSourceData, sourceData.Length);

                    using var zLibStream = new ZLibStream(sourceStream, CompressionMode.Decompress);

                    if (zLibStream.BaseStream.Length < 0)
                        return -1;

                    zLibStream.CopyTo(destStream);

                    return (int)destStream.Position;
                }
            }
        }
        else
        {
            sourceData.Slice(1).CopyTo(destData);

            return sourceData.Length - 1;
        }
    }

    protected override unsafe int EncryptUserSupplied(Span<byte> destData, Span<byte> sourceData)
    {
        // Don't bother compressing
        if (sourceData.Length < 24)
        {
            destData[0] = 0;

            sourceData.CopyTo(destData.Slice(1));

            return sourceData.Length + 1;
        }

        // I don't like the fact we have to use unsafe code here but I couldn't
        // find another way to create a stream without copying the data.
        fixed (byte* pDestData = &destData[1])
        {
            fixed (byte* pSourceData = &sourceData[0])
            {
                using var destStream = new UnmanagedMemoryStream(pDestData, destData.Length);
                using var sourceStream = new UnmanagedMemoryStream(pSourceData, sourceData.Length);

                using (var zLibStream = new ZLibStream(destStream, CompressionMode.Compress, true))
                {
                    destData[0] = 1;

                    sourceStream.CopyTo(zLibStream);
                }

                // Compressing ended up being worse
                if (destStream.Position > sourceData.Length)
                {
                    destData[0] = 0;

                    sourceData.CopyTo(destData.Slice(1));

                    return sourceData.Length + 1;
                }

                return (int)destStream.Position + 1;
            }
        }
    }

    #endregion
}