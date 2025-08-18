using System;
using System.Net;

using Sanctuary.UdpLibrary.Configuration;
using Sanctuary.UdpLibrary.Enumerations;
using Sanctuary.UdpLibrary.Packets;

namespace Sanctuary.UdpLibrary.Abstractions;

public interface IUdpManager
{
    long CachedClock { get; }
    UdpParams Params { get; set; }

    UdpClockStamp ProcessingInducedLag { get; }

    uint LocalSyncStampLong();
    ushort LocalSyncStampShort();

    long CachedClockElapsed(long start);

    int Random();

    void ActualSend(ReadOnlySpan<byte> data, int dataLen, SocketAddress socketAddress);

    void SendPortAlive(SocketAddress socketAddress);

    void SetPriority(UdpConnection con, long stamp);

    LogicalPacket CreatePacket(Span<byte> data, int dataLen, Span<byte> data2 = default, int dataLen2 = 0);

    void CallbackRoutePacket(UdpConnection con, Span<byte> data);
    void CallbackCrcReject(UdpConnection con, Span<byte> data);
    void CallbackPacketCorrupt(UdpConnection con, Span<byte> data, UdpCorruptionReason reason);
    void CallbackConnectComplete(UdpConnection con);

    void KeepUntilDisconnected(UdpConnection udpConnection);

    void RemoveConnection(UdpConnection con);

    void CallbackTerminated(UdpConnection con);

    void IncrementCrcRejectedPackets();
    void IncrementOrderRejectedPackets();
    void IncrementDuplicatePacketsReceived();
    void IncrementResentPacketsAccelerated();
    void IncrementResentPacketsTimedOut();
    void IncrementApplicationPacketsSent();
    void IncrementApplicationPacketsReceived();
    void IncrementCorruptPacketErrors();
}