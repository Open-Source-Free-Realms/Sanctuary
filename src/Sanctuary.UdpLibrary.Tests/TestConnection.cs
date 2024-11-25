using System;
using System.Net;
using System.Diagnostics;

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

    protected override int DecryptUserSupplied(Span<byte> destData, Span<byte> sourceData)
    {
        if (sourceData[0] == 1)
        {
            return ZLib.Decompress(sourceData.Slice(1), destData);
        }
        else
        {
            sourceData.Slice(1).CopyTo(destData);

            return sourceData.Length - 1;
        }
    }

    protected override int EncryptUserSupplied(Span<byte> destData, Span<byte> sourceData)
    {
        if (sourceData.Length >= 24)
        {
            var compressedLength = ZLib.Compress(sourceData, destData.Slice(1));

            if (compressedLength > 0 && compressedLength < sourceData.Length)
            {
                destData[0] = 1;

                return compressedLength + 1;
            }
        }

        destData[0] = 0;

        sourceData.CopyTo(destData.Slice(1));

        return sourceData.Length + 1;
    }

    #endregion
}