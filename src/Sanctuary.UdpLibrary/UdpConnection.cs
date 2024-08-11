using System;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

using Sanctuary.Core.IO;
using Sanctuary.UdpLibrary.Internal;
using Sanctuary.UdpLibrary.Abstractions;
using Sanctuary.UdpLibrary.Enumerations;
using Sanctuary.UdpLibrary.Packets;
using Sanctuary.UdpLibrary.Statistics;
using System.Threading;

namespace Sanctuary.UdpLibrary;

/// <summary>
/// The purpose of the UdpConnection is to manage a single logical connection
/// </summary>
public class UdpConnection : PriorityQueueMember
{
    private readonly Lock _guard = new();
    private readonly Lock _handlerGuard = new();

    public IPEndPoint EndPoint { get; internal set; }
    public SocketAddress SocketAddress { get; internal set; }

    public Status Status { get; private set; }

    public DisconnectReason DisconnectReason { get; private set; }
    public DisconnectReason OtherSideDisconnectReason { get; private set; }

    internal IUdpManager UdpManager;

    internal int ConnectCode;

    internal UdpConnectionStatistics ConnectionStats;

    private UdpClockStamp ConnectionCreateTime;
    private UdpClockStamp ConnectAttemptTimeout;
    public UdpClockStamp NoDataTimeout;

    private bool FlaggedPortUnreachable;
    private bool SilentDisconnect;

    private UdpReliableChannel[] Channel;

    internal class Configuration
    {
        public int EncryptCode;
        public byte CrcBytes;
        public EncryptMethod[] EncryptMethod = new EncryptMethod[Constants.EncryptPasses];
        public int MaxRawPacketSize; // negotiated maxRawPacketSize (ie. smaller of what two sides are set to)
    }

    internal Configuration ConnectionConfig = new();

    private int OtherSideProtocolVersion;
    private string? OtherSideProtocolName;

    private UdpClockStamp LastClockSyncTime;
    private UdpClockStamp DataHoldTime;
    private UdpClockStamp LastSendTime;
    private UdpClockStamp LastReceiveTime;
    private UdpClockStamp LastPortAliveTime;

    private byte[] MultiBufferData;
    private int MultiBufferOffset;

    private int OrderedCountOutgoing;
    private int OrderedCountOutgoing2;
    private ushort OrderedStampLast;
    private ushort OrderedStampLast2;

    private byte[]? _encryptXorBuffer;
    internal int EncryptExpansionBytes;

    private uint SyncTimeDelta;
    private uint SyncStatTotal;
    private uint SyncStatCount;
    private uint SyncStatLow;
    private uint SyncStatHigh;
    private uint SyncStatLast;
    private uint SyncStatMasterRoundTime;
    private UdpClockStamp SyncStatMasterFixupTime;

    private bool GettingTime;

    private int KeepAliveDelay;

    private UdpClockStamp IcmpErrorRetryStartStamp;
    private UdpClockStamp PortRemapRequestStartStamp;

    private UdpClockStamp DisconnectFlushStamp;
    private UdpClockStamp DisconnectFlushTimeout;

    private delegate int CryptFunction(Span<byte> destData, Span<byte> sourceData);

    private readonly byte[][] _tempDecryptBuffer;
    private readonly byte[][] _tempEncryptBuffer;
    private CryptFunction[] DecryptFunction = new CryptFunction[Constants.EncryptPasses];
    private CryptFunction[] EncryptFunction = new CryptFunction[Constants.EncryptPasses];

    // Client-side initialization
    public UdpConnection(IUdpManager udpManager, SocketAddress socketAddress, UdpClockStamp timeout) : this(udpManager, socketAddress)
    {
        lock (_guard)
        {
            ConnectAttemptTimeout = timeout;

            Status = Status.Negotiating;

            ConnectCode = UdpManager.Random();

            GiveTime();
        }
    }

    // Server-side initialization
    public UdpConnection(IUdpManager udpManager, SocketAddress socketAddress, int connectCode) : this(udpManager, socketAddress)
    {
        lock (_guard)
        {
            Status = Status.Connected;

            ConnectionConfig.EncryptMethod = new EncryptMethod[Constants.EncryptPasses];

            for (var i = 0; i < Constants.EncryptPasses; i++)
                ConnectionConfig.EncryptMethod[i] = UdpManager.Params.EncryptMethod[i];

            ConnectionConfig.CrcBytes = UdpManager.Params.CrcBytes;
            ConnectionConfig.MaxRawPacketSize = UdpManager.Params.MaxRawPacketSize;
            ConnectionConfig.EncryptCode = UdpManager.Random();

            SetupEncryptModel();

            ConnectCode = connectCode;
        }
    }

    private UdpConnection(IUdpManager udpManager, SocketAddress socketAddress)
    {
        UdpManager = udpManager;

        SocketAddress = new SocketAddress(socketAddress.Family, socketAddress.Size);
        socketAddress.Buffer.CopyTo(SocketAddress.Buffer);

        var tempEndPoint = new IPEndPoint(IPAddress.Any, 0);
        EndPoint = (IPEndPoint)tempEndPoint.Create(socketAddress);

        FlaggedPortUnreachable = false;

        // makes it send out the first connect packet immediately (if we are in negotiating mode)
        LastPortAliveTime = LastSendTime = 0;
        LastReceiveTime = UdpManager.CachedClock;
        LastClockSyncTime = 0;
        DataHoldTime = 0;
        GettingTime = false;
        OtherSideProtocolVersion = 0;
        OtherSideProtocolName = null;

        NoDataTimeout = UdpManager.Params.NoDataTimeout;
        KeepAliveDelay = UdpManager.Params.KeepAliveDelay;

        MultiBufferData = GC.AllocateArray<byte>(UdpManager.Params.MaxRawPacketSize, true);
        MultiBufferOffset = 0;

        // when the timer started for ICMP error retry delay (gets reset on a successful packet receive)
        IcmpErrorRetryStartStamp = 0;
        PortRemapRequestStartStamp = 0;

        _encryptXorBuffer = null;
        EncryptExpansionBytes = 0;
        OrderedCountOutgoing = 0;
        OrderedCountOutgoing2 = 0;
        OrderedStampLast = 0;
        OrderedStampLast2 = 0;
        DisconnectReason = DisconnectReason.None;
        OtherSideDisconnectReason = DisconnectReason.None;

        ConnectAttemptTimeout = 0;
        ConnectionCreateTime = UdpManager.CachedClock;
        SilentDisconnect = false;

        PingStatReset();
        SyncTimeDelta = 0;

        Channel = new UdpReliableChannel[Constants.ReliableChannelCount];

        _tempDecryptBuffer = new byte[Constants.EncryptPasses][];
        _tempEncryptBuffer = new byte[Constants.EncryptPasses][];

        for (var i = 0; i < Constants.EncryptPasses; i++)
        {
            _tempDecryptBuffer[i] = GC.AllocateArray<byte>(Constants.HardMaxRawPacketSize, true);
            _tempEncryptBuffer[i] = GC.AllocateArray<byte>(Constants.HardMaxRawPacketSize + sizeof(int), true);
        }
    }

    private void PortUnreachable()
    {
        if (!UdpManager.Params.ProcessIcmpErrors)
            return;

        if (!UdpManager.Params.ProcessIcmpErrorsDuringNegotiating)
        {
            // during negotiating phase, ignore port unreachable errors, since it may be a case of the client starting up first
            if (Status == Status.Negotiating)
                return;
        }

        if (UdpManager.Params.IcmpErrorRetryPeriod != 0)
        {
            if (IcmpErrorRetryStartStamp == 0)
            {
                // start timer on how long we will ignore ICMP errors
                IcmpErrorRetryStartStamp = UdpManager.CachedClock;
                return;
            }

            if (UdpManager.CachedClockElapsed(IcmpErrorRetryStartStamp) < UdpManager.Params.IcmpErrorRetryPeriod)
            {
                return;        // ignoring ICMP errors for a period of time
            }
        }

        InternalDisconnect(0, DisconnectReason.IcmpError);
    }

    internal void InternalDisconnect(int flushTimeout, DisconnectReason reason)
    {
        lock (_guard)
        {
            if (DisconnectReason == DisconnectReason.None)
                DisconnectReason = reason;

            // if we are in a negotiating state, then you can't have a flushTimeout, any disconnect will occur immediately
            if (Status == Status.Negotiating)
                flushTimeout = 0;

            if (UdpManager is null)
                return;

            if (flushTimeout > 0)
            {
                FlushMultiBuffer();

                DisconnectFlushStamp = UdpManager.CachedClock;
                DisconnectFlushTimeout = flushTimeout;

                ScheduleTimeNow();

                if (Status != Status.DisconnectPending)
                {
                    Status = Status.DisconnectPending;
                    UdpManager.KeepUntilDisconnected(this);
                }

                return;
            }

            // send a termination packet to the other side
            // do not send a termination packet if we are still negotiating (we are not allowed to send any packets while negotiating)
            // if you attempt to send a packet while negotiating, then it will potentially attempt to encrypt it before an encryption
            // method is determined, resulting in a function call through an invalid pointer
            if (!SilentDisconnect)
            {
                if (Status is Status.Connected or Status.DisconnectPending)
                {
                    SendTerminatePacket(ConnectCode, DisconnectReason);
                }
            }

            Status = Status.Disconnected;

            UdpManager.RemoveConnection(this);

            if (reason != DisconnectReason.ManagerDeleted)
                UdpManager.CallbackTerminated(this);
        }
    }

    private void SendTerminatePacket(int connectCode, DisconnectReason reason)
    {
        Span<byte> buf = stackalloc byte[8 + 4];

        buf[0] = 0;
        buf[1] = (byte)UdpPacketType.Terminate;

        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(2), connectCode);
        BinaryPrimitives.WriteInt16BigEndian(buf.Slice(6), (short)reason);

        PhysicalSend(buf, 8, true);
    }

    public bool Send(UdpChannel channel, Span<byte> data)
    {
        lock (_guard)
        {
            Debug.Assert(data.Length >= 0);

            // if we are no longer connected (not allowed to send more when we are pending disconnect either)
            if (Status != Status.Connected)
                return false;

            // zero length packets are ignored
            if (data.IsEmpty)
                return false;

            // zero-escape application packets that start with 0
            if (data[0] == 0)
            {
                Span<byte> hold = [0];
                return InternalSend(channel, hold, hold.Length, data, data.Length);
            }

            return InternalSend(channel, data, data.Length);
        }
    }

    public bool Send(UdpChannel channel, LogicalPacket packet)
    {
        lock (_guard)
        {
            // if we are no longer connected
            if (Status != Status.Connected)
                return false;

            var dataLen = packet.GetDataLen();
            if (dataLen == 0)
                return false;

            // zero-escape application packets that start with 0
            var data = packet.GetDataPtr();
            if (data[0] == 0)
            {
                Span<byte> hold = [0];
                return InternalSend(channel, hold, hold.Length, data, dataLen);
            }

            return InternalSend(channel, data, dataLen);
        }
    }

    private bool InternalSend(UdpChannel channel, Span<byte> data, int dataLen, Span<byte> data2 = default, int dataLen2 = 0)
    {
        Debug.Assert(channel >= 0 && channel < UdpChannel.Count);
        Debug.Assert(Status != Status.Negotiating);

        UdpManager.IncrementApplicationPacketsSent();
        ConnectionStats.ApplicationPacketsSent++;

        // promote unreliable packets that are larger than maxRawPacketSize to be reliable
        var totalDataLen = dataLen + dataLen2;

        var rawDataBytesMax = ConnectionConfig.MaxRawPacketSize - ConnectionConfig.CrcBytes - EncryptExpansionBytes;

        if ((channel is UdpChannel.Unreliable or UdpChannel.UnreliableUnbuffered) && totalDataLen > rawDataBytesMax)
            channel = UdpChannel.Reliable1;
        else if ((channel is UdpChannel.Ordered or UdpChannel.OrderedUnbuffered) && totalDataLen > rawDataBytesMax - Constants.UdpPacketOrderedSize)
            channel = UdpChannel.Reliable1;

        switch (channel)
        {
            case UdpChannel.Unreliable:
                BufferedSend(data, dataLen, data2, dataLen2, false);
                return true;

            case UdpChannel.UnreliableUnbuffered:
                {
                    var tempBuffer = new byte[Constants.HardMaxRawPacketSize];

                    var bufPtr = tempBuffer.AsSpan();

                    data.CopyTo(bufPtr);

                    if (!data2.IsEmpty)
                        data2.CopyTo(bufPtr.Slice(data.Length));

                    PhysicalSend(bufPtr, totalDataLen, true);

                    return true;
                }

            case UdpChannel.Ordered:
                {
                    var tempBuffer = new byte[Constants.HardMaxRawPacketSize];

                    var bufPtr = tempBuffer.AsSpan();

                    bufPtr[0] = 0;
                    bufPtr[1] = (byte)UdpPacketType.Ordered;

                    BinaryPrimitives.WriteUInt16BigEndian(bufPtr.Slice(2), (ushort)++OrderedCountOutgoing);

                    data.CopyTo(bufPtr.Slice(4));

                    if (!data2.IsEmpty)
                        data2.CopyTo(bufPtr.Slice(4 + data.Length));

                    BufferedSend(bufPtr, totalDataLen + 4, null, 0, true);

                    return true;
                }

            case UdpChannel.OrderedUnbuffered:
                {
                    var tempBuffer = new byte[Constants.HardMaxRawPacketSize];

                    var bufPtr = tempBuffer.AsSpan();

                    bufPtr[0] = 0;
                    bufPtr[1] = (byte)UdpPacketType.Ordered2;

                    BinaryPrimitives.WriteUInt16BigEndian(bufPtr.Slice(2), (ushort)++OrderedCountOutgoing2);

                    data.CopyTo(bufPtr.Slice(4));

                    if (!data2.IsEmpty)
                        data2.CopyTo(bufPtr.Slice(data.Length + 4));

                    PhysicalSend(bufPtr, totalDataLen + 4, true);

                    return true;
                }

            case UdpChannel.Reliable1:
            case UdpChannel.Reliable2:
            case UdpChannel.Reliable3:
            case UdpChannel.Reliable4:
                {
                    var num = channel - UdpChannel.Reliable1;

                    if (Channel[num] is null)
                        Channel[num] = new UdpReliableChannel(num, this, UdpManager.Params.Reliable[num]);

                    Channel[num].Send(data, dataLen, data2, dataLen2);

                    return true;
                }
        }

        return false;
    }

    private void PingStatReset()
    {
        lock (_guard)
        {
            // tells it to resync the clock pronto
            LastClockSyncTime = 0;

            SyncStatMasterFixupTime = 0;
            SyncStatMasterRoundTime = 0;
            SyncStatLow = 0;
            SyncStatHigh = 0;
            SyncStatLast = 0;
            SyncStatTotal = 0;
            SyncStatCount = 0;
            ConnectionStats.AveragePingTime = 0;
            ConnectionStats.HighPingTime = 0;
            ConnectionStats.LowPingTime = 0;
            ConnectionStats.LastPingTime = 0;
            ConnectionStats.MasterPingTime = 0;
        }
    }

    public void GetStats(out UdpConnectionStatistics stats)
    {
        lock (_guard)
        {
            stats = ConnectionStats;

            stats.MasterPingAge = UdpManager.Params.ClockSyncDelay == 0 ? -1 : UdpManager.CachedClockElapsed(SyncStatMasterFixupTime);

            stats.PercentSentSuccess = 1.0f;
            stats.PercentReceivedSuccess = 1.0f;

            if (stats.SyncOurSent > 0)
                stats.PercentSentSuccess = stats.SyncTheirReceived / stats.SyncOurSent;

            if (stats.SyncTheirSent > 0)
                stats.PercentReceivedSuccess = stats.SyncOurReceived / stats.SyncTheirSent;

            stats.ReliableAveragePing = 0;

            if (Channel[0] is not null)
                stats.ReliableAveragePing = Channel[0].GetAveragePing();
        }
    }

    internal void ProcessRawPacket(Span<byte> data)
    {
        lock (_guard)
        {
            if (data.Length == 0)
            {
                // length 0 packet indicates an ICMP dest-unreachable error packet on this connection (the driver puts these errors inline as 0 byte packets)
                PortUnreachable();
                return;
            }

            var reader = new PacketReader(data);

            if (!reader.TryRead(out byte zeroByte))
                return;

            if (!reader.TryRead(out UdpPacketType packetType))
                return;

            if (zeroByte != 0 || packetType != UdpPacketType.UnreachableConnection)
            {
                // if we get any type of packet other than an unreachable-connection packet, then we can assume that our remapping
                // request succeeded, and clear the timer for how long we should attempt to do the remapping.  The reason we need
                // to send requests for a certain amount of time, is the server may already have dozens of unreachable-connection packets
                // on the wire on the way to us, before we manage to request that the remapping occur.
                PortRemapRequestStartStamp = 0;
            }

            // we received a packet successfully, so assume we have recovered from any ICMP error state we may have been in, so we can reset the timer
            IcmpErrorRetryStartStamp = 0;

            LastReceiveTime = UdpManager.CachedClock;

            ConnectionStats.TotalPacketsReceived++;
            ConnectionStats.TotalBytesReceived += data.Length;

            // TODO
            // track incoming data rate

            if (zeroByte == 0 && packetType == UdpPacketType.KeepAlive)
            {
                // encryption can't mess up the first two bytes of an internal packet, so this is safe to check
                // if it is a keep alive packet, then we don't need to do any more processing beyond setting
                // the mLastReceiveTime.  We do this check here instead of letting it pass on through harmlessly
                // like we used to do in order to avoid getting rescheduled in the priority queue.  There is absolutely
                // no reason to reschedule us due to an incoming keep alive packet since the keep-alive packet has the
                // longest rescheduling of anything that needs time, so the worst thing that might happen is we might
                // end up getting sheduled time sooner than we might otherwise need to.  And obviously scheduling
                // ourselves for immediate-time is even sooner than that, so there is no point.
                // This turns out to be important for applications that have lots of connections (tens of thousands)
                // that rarely talk but send keep alives...no reason to make the server do a lot work over these things.
                return;
            }

            // whenever we receive a packet, it could potentially change when we want time scheduled again
            // so effectively we should reprioritize ourself to the top.  By doing it this way instead of
            // simply giving time and recalculating, we can effectively avoid giving ourself time and reprioritizing
            // ourself over and over again as more and more packets arrive in rapid succession
            // note: this cannot happen while we are in our UdpConnection::GiveTime function, so there is no need to squeltch check 
            // it like we do the others.
            // note: this was moved to the top of the function from the bottom.  This doesn't effect anything as it doesn't matter
            // when we schedule ourself for future processing.  Moving it to the top allowed us to get scheduled even if the packet
            // we processed got rejected for some reason (crc mismatch or bad size).
            ScheduleTimeNow();

            // invalid packet len
            if (data.Length < 1)
            {
                CallbackCorruptPacket(data, UdpCorruptionReason.ZeroLengthPacket);
                return;
            }

            // first see if we are a special connect/confirm/unreachable packet, if so, process us immediately
            if (zeroByte == 0 && IsNonEncryptPacket(packetType))
            {
                ProcessCookedPacket(data);
            }
            else
            {
                // if we are still awaiting confirmation packet, then we must ignore any other incoming data packets
                // this can happen if the confirm packet is lost and the server has dumped a load of data on the newly created connection
                if (Status == Status.Negotiating)
                    return;

                var finalStart = data;
                var finalLen = data.Length;

                if (ConnectionConfig.CrcBytes > 0)
                {
                    if (finalLen < ConnectionConfig.CrcBytes)
                    {
                        // invalid packet len
                        CallbackCorruptPacket(data, UdpCorruptionReason.PacketShorterThanCrcBytes);
                        return;
                    }

                    var crcPtr = finalStart.Slice(finalLen - ConnectionConfig.CrcBytes);

                    var wantCrc = 0u;
                    var actualCrc = UdpMisc.Crc32(finalStart, finalLen - ConnectionConfig.CrcBytes, ConnectionConfig.EncryptCode);

                    switch (ConnectionConfig.CrcBytes)
                    {
                        case 1:
                            wantCrc = crcPtr[0];
                            actualCrc &= 0xff;
                            break;

                        case 2:
                            wantCrc = BinaryPrimitives.ReadUInt16BigEndian(crcPtr);
                            actualCrc &= 0xffff;
                            break;

                        case 3:
                            wantCrc = UdpMisc.GetValue24(crcPtr);
                            actualCrc &= 0xffffff;
                            break;

                        case 4:
                            wantCrc = BinaryPrimitives.ReadUInt32BigEndian(crcPtr);
                            break;
                    }

                    if (wantCrc != actualCrc)
                    {
                        ConnectionStats.CrcRejectedPackets++;

                        UdpManager.IncrementCrcRejectedPackets();
                        UdpManager.CallbackCrcReject(this, data);

                        return;
                    }

                    finalLen -= ConnectionConfig.CrcBytes;
                }

                for (var i = Constants.EncryptPasses - 1; i >= 0; i--)
                {
                    if (ConnectionConfig.EncryptMethod[i] == EncryptMethod.None)
                        continue;

                    var decryptPtr = _tempDecryptBuffer[i].AsSpan();

                    decryptPtr[0] = finalStart[0];

                    if (finalStart[0] == 0)
                    {
                        if (finalLen < 2)
                        {
                            // invalid packet len
                            CallbackCorruptPacket(data, UdpCorruptionReason.InternalPacketTooShort);
                            return;
                        }

                        decryptPtr[1] = finalStart[1];

                        var len = DecryptFunction[i](decryptPtr.Slice(2), finalStart.Slice(2, finalLen - 2));

                        if (len == -1)
                        {
                            // decrypt failed, throw away packet
                            CallbackCorruptPacket(data, UdpCorruptionReason.DecryptFailed);
                            return;
                        }

                        finalLen = len + 2;
                    }
                    else
                    {
                        var len = DecryptFunction[i](decryptPtr.Slice(1), finalStart.Slice(1, finalLen - 1));

                        if (len == -1)
                        {
                            // decrypt failed, throw away packet
                            CallbackCorruptPacket(data, UdpCorruptionReason.DecryptFailed);
                            return;
                        }

                        finalLen = len + 1;
                    }

                    finalStart = _tempDecryptBuffer[i];
                }

                ProcessCookedPacket(finalStart.Slice(0, finalLen));
            }
        }
    }

    internal void CallbackRoutePacket(Span<byte> data)
    {
        if (Status != Status.Connected)
            return;

        UdpManager.IncrementApplicationPacketsReceived();

        ConnectionStats.ApplicationPacketsReceived++;

        // callback through UdpManager in case it wants to queue the event
        UdpManager.CallbackRoutePacket(this, data);
    }

    internal void CallbackCorruptPacket(Span<byte> data, UdpCorruptionReason reason)
    {
        if (Status != Status.Connected)
            return;

        ConnectionStats.CorruptPacketErrors++;
        UdpManager.IncrementCorruptPacketErrors();

        UdpManager.CallbackPacketCorrupt(this, data, reason);

        InternalDisconnect(0, DisconnectReason.CorruptPacket);
    }

    internal void ProcessCookedPacket(Span<byte> data)
    {
        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte zeroByte))
            return;

        if (zeroByte != 0 || data.Length <= 1)
        {
            CallbackRoutePacket(data);
            return;
        }

        // internal packet, so process it internally

        if (!reader.TryRead(out byte packetType))
            return;

        switch ((UdpPacketType)packetType)
        {
            case UdpPacketType.Connect:
                {
                    if (!reader.TryReadInt32(out var otherSideProtocolVersion))
                        return;

                    if (!reader.TryReadInt32(out var connectCode))
                        return;

                    if (!reader.TryReadInt32(out var maxRawPacketSize))
                        return;

                    var otherSideProtocolName = string.Empty;

                    if (otherSideProtocolVersion > 2 && data.Length > 14)
                    {
                        for (var i = 0; i < 32; i++)
                        {
                            if (!reader.TryRead(out byte protocolChar) || protocolChar == 0)
                                break;

                            otherSideProtocolName += (char)protocolChar;
                        }
                    }

                    if (Status == Status.Negotiating)
                    {
                        // why are we receiving a connect-request coming from the guy we ourselves are currently
                        // in the process of trying to connect to?  Odds are very high that what is actually
                        // happening is we are trying to connect to ourself.  In either case, we should reply
                        // back telling them they are terminated.

                        SendTerminatePacket(connectCode, ConnectCode == connectCode
                            ? DisconnectReason.ConnectingToSelf
                            : DisconnectReason.MutualConnectError);
                    }
                    else if (ConnectCode == connectCode)
                    {
                        OtherSideProtocolVersion = otherSideProtocolVersion;

                        if (!string.IsNullOrEmpty(otherSideProtocolName))
                            OtherSideProtocolName = otherSideProtocolName;

                        ConnectionConfig.MaxRawPacketSize = Math.Min(maxRawPacketSize, ConnectionConfig.MaxRawPacketSize);

                        Span<byte> buf = stackalloc byte[21];

                        // send confirm packet (if our connect code matches up)
                        // prepare UdpPacketConnect packet

                        buf[0] = 0;
                        buf[1] = (byte)UdpPacketType.Confirm;
                        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(2), ConnectCode);
                        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(6), ConnectionConfig.EncryptCode);
                        buf[10] = ConnectionConfig.CrcBytes;

                        for (var i = 0; i < Constants.EncryptPasses; i++)
                            buf[11 + i] = (byte)ConnectionConfig.EncryptMethod[i];

                        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(13), ConnectionConfig.MaxRawPacketSize);
                        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(17), Constants.ProtocolVersion);

                        RawSend(buf, buf.Length);

                        if (!string.IsNullOrEmpty(UdpManager.Params.ProtocolName) && !string.Equals(UdpManager.Params.ProtocolName, OtherSideProtocolName))
                            InternalDisconnect(0, DisconnectReason.OtherProtocolName);
                    }
                    else
                    {
                        // ok, we got a connect-request packet from the ip/port of something we thought we already had a connection to.
                        // Additionally, the connect-request packet has a different code, meaning it is not just a stragling connect-request
                        // packet that got sent after we accepted the connection.
                        // This means that the other side has probably terminated the connection and is attempting to connect again.
                        // if we just ignore the new connect-request, it will actually result in the new connection-attempt effectively
                        // keeping this connection object alive.  So, instead, when we get this situation, we will terminate this connection
                        // and ignore the connect-request packet.  The connect-request packet will be sent again 1 second later by the client
                        // at which time we won't exist and out UdpManager will establish a new connection object for it.
                        SendTerminatePacket(0, DisconnectReason.NewConnectionAttempt);
                    }
                }
                break;

            case UdpPacketType.Confirm:
                {
                    var config = new Configuration();

                    var otherSideProtocolVersion = 0;

                    if (!reader.TryReadInt32(out var connectCode))
                        return;

                    if (!reader.TryReadInt32(out config.EncryptCode))
                        return;

                    if (!reader.TryRead(out config.CrcBytes))
                        return;

                    for (var i = 0; i < Constants.EncryptPasses; i++)
                    {
                        if (!reader.TryRead(out byte encryptMethod))
                            return;

                        config.EncryptMethod[i] = (EncryptMethod)encryptMethod;
                    }

                    if (!reader.TryReadInt32(out config.MaxRawPacketSize))
                        return;

                    if (reader.RemainingLength > 0)
                    {
                        if (!reader.TryReadInt32(out otherSideProtocolVersion))
                            return;
                    }

                    // only actually process the confirm if we are negotiating (expecting it) and the connect-code matches up
                    if (Status == Status.Negotiating && ConnectCode == connectCode)
                    {
                        ConnectionConfig = config;
                        OtherSideProtocolVersion = otherSideProtocolVersion;
                        SetupEncryptModel();
                        Status = Status.Connected;
                        UdpManager.CallbackConnectComplete(this);
                    }
                }
                break;

            // if a request remap packet managed to get routed to our connection, it is because
            // the mapping is already correct, so we can just ignore this packet at this point
            // this will happen when the client sends multiple remap-requests, the first one will
            // cause the actual remapping to occur, and the subsequent ones will manage to make
            // it into here
            case UdpPacketType.RequestRemap:
                break;

            case UdpPacketType.ZeroEscape:
                CallbackRoutePacket(data.Slice(1));
                break;

            case UdpPacketType.Ordered:
                {
                    if (!reader.TryReadUInt16(out var orderedStamp))
                        return;

                    var diff = orderedStamp - OrderedStampLast;

                    // equal here makes it strip dupes too
                    if (diff <= 0)
                        diff += 0x10000;

                    if (diff < 30000)
                    {
                        OrderedStampLast = orderedStamp;
                        CallbackRoutePacket(data.Slice(Constants.UdpPacketOrderedSize));
                    }
                    else
                    {
                        ConnectionStats.OrderRejectedPackets++;
                        UdpManager.IncrementOrderRejectedPackets();
                    }
                }
                break;

            case UdpPacketType.Ordered2:
                {
                    if (!reader.TryReadUInt16(out var orderedStamp))
                        return;

                    var diff = orderedStamp - OrderedStampLast2;

                    // equal here makes it strip dupes too
                    if (diff <= 0)
                        diff += 0x10000;

                    if (diff < 30000)
                    {
                        OrderedStampLast2 = orderedStamp;
                        CallbackRoutePacket(data.Slice(Constants.UdpPacketOrderedSize));
                    }
                    else
                    {
                        ConnectionStats.OrderRejectedPackets++;
                        UdpManager.IncrementOrderRejectedPackets();
                    }
                }
                break;

            case UdpPacketType.Terminate:
                {
                    if (!reader.TryReadInt32(out var connectCode))
                        return;

                    // to remain protocol compatible with previous version, the other side disconnect reason is an optional field on this packet
                    if (reader.RemainingLength > 0)
                    {
                        if (!reader.TryReadInt16(out var otherSideDisconnectReason))
                            return;

                        OtherSideDisconnectReason = (DisconnectReason)otherSideDisconnectReason;
                    }

                    if (ConnectCode == connectCode)
                    {
                        // since other side explicitly told us they had terminated, there is no reason for us to send a terminate
                        // packet back to them as well (as it will almost always result in some for of unreachable-destination reply)
                        // so, put ourselves in silent-disconnect mode when this happens
                        SilentDisconnect = true;
                        InternalDisconnect(0, DisconnectReason.OtherSideTerminated);
                        return;
                    }
                }
                break;

            case UdpPacketType.UnreachableConnection:
                {
                    if (UdpManager.Params.AllowPortRemapping)
                    {
                        if (PortRemapRequestStartStamp == 0)
                        {
                            PortRemapRequestStartStamp = UdpManager.CachedClock;
                        }

                        if (UdpManager.CachedClockElapsed(PortRemapRequestStartStamp) < Constants.MaximumTimeAllowedForPortRemapping)
                        {
                            Span<byte> buf = stackalloc byte[21];

                            // send confirm packet (if our connect code matches up)
                            // prepare UdpPacketConnect packet

                            buf[0] = 0;
                            buf[1] = (byte)UdpPacketType.RequestRemap;
                            BinaryPrimitives.WriteInt32BigEndian(buf.Slice(2), ConnectCode);
                            BinaryPrimitives.WriteInt32BigEndian(buf.Slice(6), ConnectionConfig.EncryptCode);

                            RawSend(buf, buf.Length); // since the destination doesn't have an associated connection for us to decrypt us, we must be sent unencrypted

                            break;
                        }
                    }

                    InternalDisconnect(0, DisconnectReason.UnreachableConnection);
                }
                return;

            case UdpPacketType.Multi:
                {
                    var ptr = 2;
                    var endPtr = data.Length;

                    while (ptr < endPtr)
                    {
                        var len = data[ptr++];

                        var nextPtr = ptr + len;

                        if (nextPtr > endPtr)
                        {
                            // specified more data in this piece than is left in the entire packet
                            // this is either corruption, or more likely a hacker
                            CallbackCorruptPacket(data, UdpCorruptionReason.MisformattedGroup);
                            return;
                        }

                        ProcessCookedPacket(data.Slice(ptr, len));

                        ptr = nextPtr;
                    }

                    Debug.Assert(ptr == endPtr);
                }
                break;

            case UdpPacketType.ClockSync:
                {
                    UdpPacketClockSync pp;

                    if (!reader.TryReadUInt16(out pp.TimeStamp) ||
                        !reader.TryReadUInt32(out pp.MasterPingTime) ||
                        !reader.TryReadUInt32(out pp.AveragePingTime) ||
                        !reader.TryReadUInt32(out pp.LowPingTime) ||
                        !reader.TryReadUInt32(out pp.HighPingTime) ||
                        !reader.TryReadUInt32(out pp.LastPingTime) ||
                        !reader.TryReadInt64(out pp.OurSent) ||
                        !reader.TryReadInt64(out pp.OurReceived))
                        return;

                    ConnectionStats.AveragePingTime = pp.AveragePingTime;
                    ConnectionStats.HighPingTime = pp.HighPingTime;
                    ConnectionStats.LowPingTime = pp.LowPingTime;
                    ConnectionStats.LastPingTime = pp.LastPingTime;
                    ConnectionStats.MasterPingTime = pp.MasterPingTime;
                    ConnectionStats.SyncOurReceived = ConnectionStats.TotalPacketsReceived;
                    ConnectionStats.SyncOurSent = ConnectionStats.TotalPacketsSent;
                    ConnectionStats.SyncTheirReceived = pp.OurReceived;
                    ConnectionStats.SyncTheirSent = pp.OurSent;

                    // if it has been over a second since our manager got processing time, then we should not reflect clock-sync packets as we will have introduced too much lag ourselves.
                    if (UdpManager.ProcessingInducedLag > 1000)
                        break;

                    // prepare UdpPacketClockReflect packet
                    Span<byte> buf = stackalloc byte[40 + 4];

                    buf[0] = 0;
                    buf[1] = (byte)UdpPacketType.ClockReflect;

                    BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(2), pp.TimeStamp);
                    BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(4), UdpManager.LocalSyncStampLong());
                    BinaryPrimitives.WriteInt64BigEndian(buf.Slice(8), pp.OurSent);
                    BinaryPrimitives.WriteInt64BigEndian(buf.Slice(16), pp.OurReceived);
                    BinaryPrimitives.WriteInt64BigEndian(buf.Slice(24), ConnectionStats.TotalPacketsSent);
                    BinaryPrimitives.WriteInt64BigEndian(buf.Slice(32), ConnectionStats.TotalPacketsReceived);

                    PhysicalSend(buf, 40, true);
                }
                break;

            case UdpPacketType.ClockReflect:
                {
                    UdpPacketClockReflect pp;

                    if (!reader.TryReadUInt16(out pp.TimeStamp) ||
                        !reader.TryReadUInt32(out pp.ServerSyncStampLong) ||
                        !reader.TryReadInt64(out pp.YourSent) ||
                        !reader.TryReadInt64(out pp.YourReceived) ||
                        !reader.TryReadInt64(out pp.OurSent) ||
                        !reader.TryReadInt64(out pp.OurReceived))
                        return;

                    ConnectionStats.SyncOurReceived = pp.YourReceived;
                    ConnectionStats.SyncOurSent = pp.YourSent;
                    ConnectionStats.SyncTheirReceived = pp.OurReceived;
                    ConnectionStats.SyncTheirSent = pp.OurSent;

                    // if it has been over a second since our manager got processing time, then we should ignore the timing aspects of the clock-sync packets as we will have introduced too much lag ourselves.
                    if (UdpManager.ProcessingInducedLag > 1000)
                        break;

                    var curStamp = UdpManager.LocalSyncStampShort();
                    var roundTime = UdpMisc.SyncStampShortDeltaTime(pp.TimeStamp, curStamp);

                    SyncStatCount++;
                    SyncStatTotal += roundTime;

                    if (SyncStatLow == 0 || roundTime < SyncStatLow)
                        SyncStatLow = roundTime;

                    if (roundTime > SyncStatHigh)
                        SyncStatHigh = roundTime;

                    SyncStatLast = roundTime;

                    // see if we should use this sync to reset the master sync time
                    // if have better (or close to better) round time or it has been a while
                    var elapsed = UdpManager.CachedClockElapsed(SyncStatMasterFixupTime);

                    if (roundTime <= SyncStatMasterRoundTime + 20 || elapsed > 120000)
                    {
                        // resync on this packet unless this packet is a real loser (unless it just been a very long time, then sync up anyhow)
                        if (roundTime < SyncStatMasterRoundTime * 2 || elapsed > 240000)
                        {
                            SyncTimeDelta = pp.ServerSyncStampLong - UdpManager.LocalSyncStampLong() + (uint)(roundTime / 2);
                            SyncStatMasterFixupTime = UdpManager.CachedClock;
                            SyncStatMasterRoundTime = roundTime;
                        }
                    }

                    // update connection statistics
                    ConnectionStats.AveragePingTime = (SyncStatCount > 0) ? (SyncStatTotal / SyncStatCount) : 0u;
                    ConnectionStats.HighPingTime = SyncStatHigh;
                    ConnectionStats.LowPingTime = SyncStatLow;
                    ConnectionStats.LastPingTime = roundTime;
                    ConnectionStats.MasterPingTime = SyncStatMasterRoundTime;
                }
                break;

            case UdpPacketType.KeepAlive:
                break;

            case UdpPacketType.Reliable1:
            case UdpPacketType.Reliable2:
            case UdpPacketType.Reliable3:
            case UdpPacketType.Reliable4:
            case UdpPacketType.Fragment1:
            case UdpPacketType.Fragment2:
            case UdpPacketType.Fragment3:
            case UdpPacketType.Fragment4:
                {
                    var num = (packetType - (byte)UdpPacketType.Reliable1) % Constants.ReliableChannelCount;

                    if (Channel[num] is null)
                        Channel[num] = new UdpReliableChannel(num, this, UdpManager.Params.Reliable[num]);

                    Channel[num].ReliablePacket(data);
                }
                break;

            case UdpPacketType.Ack1:
            case UdpPacketType.Ack2:
            case UdpPacketType.Ack3:
            case UdpPacketType.Ack4:
                {
                    var num = packetType - (byte)UdpPacketType.Ack1;

                    Channel[num]?.AckPacket(data);
                }
                break;

            case UdpPacketType.AckAll1:
            case UdpPacketType.AckAll2:
            case UdpPacketType.AckAll3:
            case UdpPacketType.AckAll4:
                {
                    var num = packetType - (byte)UdpPacketType.AckAll1;

                    Channel[num]?.AckAllPacket(data);
                }
                break;

            case UdpPacketType.Group:
                {
                    var ptr = 2;
                    var endPtr = data.Length;

                    while (ptr < endPtr)
                    {
                        ptr += UdpMisc.GetVariableValue(data.Slice(ptr), out var len);

                        if (ptr > endPtr || len > endPtr - ptr)
                        {
                            // specified more data in this piece than is left in the entire packet
                            // this is either corruption, or more likely a hacker
                            CallbackCorruptPacket(data, UdpCorruptionReason.MisformattedGroup);
                            return;
                        }

                        ProcessCookedPacket(data.Slice(ptr, len));

                        ptr += len;
                    }
                }
                break;
        }
    }

    public void GiveTime()
    {
        lock (_guard)
        {
            GettingTime = true;

            InternalGiveTime();

            GettingTime = false;
        }
    }

    private void InternalGiveTime()
    {
        // give us time in 10 minutes (unless somebody wants it sooner)
        var nextSchedule = 10 * 60 * 1000L;

        ConnectionStats.Iterations++;

        if (FlaggedPortUnreachable)
        {
            FlaggedPortUnreachable = false;
            PortUnreachable();
        }

        switch (Status)
        {
            case Status.Negotiating:
                {
                    if (ConnectAttemptTimeout > 0 && ConnectionAge() > ConnectAttemptTimeout)
                    {
                        InternalDisconnect(0, DisconnectReason.ConnectFail);
                        return;
                    }

                    var elapsed = UdpManager.CachedClockElapsed(LastSendTime);

                    if (elapsed >= UdpManager.Params.ConnectAttemptDelay)
                    {
                        // prepare UdpPacketConnect packet

                        var protocolNameBytes = Encoding.ASCII.GetBytes(UdpManager.Params.ProtocolName);

                        Span<byte> buf = stackalloc byte[14 + protocolNameBytes.Length + 1];

                        buf[0] = 0;
                        buf[1] = (byte)UdpPacketType.Connect;
                        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(2), Constants.ProtocolVersion);
                        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(6), ConnectCode);
                        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(10), UdpManager.Params.MaxRawPacketSize);
                        protocolNameBytes.CopyTo(buf.Slice(14));
                        buf[^1] = 0;

                        RawSend(buf, buf.Length);

                        elapsed = 0;
                    }

                    nextSchedule = Math.Min(nextSchedule, UdpManager.Params.ConnectAttemptDelay - elapsed);
                }
                break;

            case Status.Connected:
            case Status.DisconnectPending:
                {
                    // sync clock if required

                    if (UdpManager.Params.ClockSyncDelay > 0)
                    {
                        // sync periodically.  If our current master round time is very bad, then sync more frequently (this is important to quickly get a sync up and running)

                        var elapsed = UdpManager.CachedClockElapsed(LastClockSyncTime);

                        if (elapsed > UdpManager.Params.ClockSyncDelay
                            || (SyncStatMasterRoundTime > 3000 && elapsed > 2000)
                            || (SyncStatMasterRoundTime > 1000 && elapsed > 5000)
                            || (SyncStatCount < 2 && elapsed > 10000))
                        {
                            // send a clock-sync packet

                            var averagePing = (SyncStatCount > 0) ? (SyncStatTotal / SyncStatCount) : 0;

                            Span<byte> buf = stackalloc byte[40 + 4];

                            buf[0] = 0;
                            buf[1] = (byte)UdpPacketType.ClockSync;
                            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(2), UdpManager.LocalSyncStampShort());
                            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(4), SyncStatMasterRoundTime);
                            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(8), averagePing);
                            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(12), SyncStatLow);
                            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(16), SyncStatHigh);
                            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(20), SyncStatLast);
                            BinaryPrimitives.WriteInt64BigEndian(buf.Slice(24), ConnectionStats.TotalPacketsSent + 1); // ourSent (add 1 to include this packet we are about to send since other side will count it as received before getting it)
                            BinaryPrimitives.WriteInt64BigEndian(buf.Slice(32), ConnectionStats.TotalPacketsReceived);

                            // don't buffer this, we need it to be as timely as possible, it still needs to be encrypted though, so don't raw send it.
                            PhysicalSend(buf, 40, true);

                            LastClockSyncTime = UdpManager.CachedClock;

                            elapsed = 0;
                        }

                        nextSchedule = Math.Min(nextSchedule, UdpManager.Params.ClockSyncDelay - elapsed);
                    }

                    // give reliable channels processing time and see when they want more time

                    var totalPendingBytes = 0;

                    for (var i = 0; i < Constants.ReliableChannelCount; i++)
                    {
                        if (Channel[i] is null)
                            continue;

                        totalPendingBytes += Channel[i].TotalPendingBytes();

                        var myNext = Channel[i].GiveTime();

                        nextSchedule = Math.Min(nextSchedule, myNext);
                    }

                    if (UdpManager.Params.ReliableOverflowBytes != 0 && totalPendingBytes >= UdpManager.Params.ReliableOverflowBytes)
                    {
                        InternalDisconnect(0, DisconnectReason.ReliableOverflow);
                        return;
                    }

                    // if we have multi-buffer data
                    if (MultiBufferOffset > 2)
                    {
                        var elapsed = UdpManager.CachedClockElapsed(DataHoldTime);

                        if (elapsed >= UdpManager.Params.MaxDataHoldTime)
                            FlushMultiBuffer(); // having just sent it, there is no data in the buffer so no reason to adjust the schedule for when it may be needed again
                        else
                            nextSchedule = Math.Min(nextSchedule, UdpManager.Params.MaxDataHoldTime - elapsed); // schedule us processing time for when it does need to be sent
                    }

                    // see if we need to keep connection alive
                    if (KeepAliveDelay > 0)
                    {
                        var elapsed = UdpManager.CachedClockElapsed(LastSendTime);

                        if (elapsed >= KeepAliveDelay)
                        {
                            // send keep-alive packet
                            Span<byte> buf = stackalloc byte[2 + 4];

                            buf[0] = 0;
                            buf[1] = (byte)UdpPacketType.KeepAlive;

                            PhysicalSend(buf, 2, true);

                            elapsed = 0;
                        }

                        // schedule us next time for a keep-alive packet
                        nextSchedule = Math.Min(nextSchedule, KeepAliveDelay - elapsed);
                    }

                    // see if we need to keep the port alive
                    if (UdpManager.Params.PortAliveDelay > 0)
                    {
                        var portElapsed = UdpManager.CachedClockElapsed(LastPortAliveTime);

                        if (portElapsed >= UdpManager.Params.PortAliveDelay)
                        {
                            LastPortAliveTime = UdpManager.CachedClock;
                            UdpManager.SendPortAlive(SocketAddress);
                            portElapsed = 0;
                        }

                        // schedule us next time for a keep-alive packet
                        nextSchedule = Math.Min(nextSchedule, UdpManager.Params.PortAliveDelay - portElapsed);
                    }

                    if (Status == Status.DisconnectPending)
                    {
                        var timeLeft = DisconnectFlushTimeout - UdpManager.CachedClockElapsed(DisconnectFlushStamp);

                        if (timeLeft < 0 || TotalPendingBytes() == 0)
                        {
                            InternalDisconnect(0, DisconnectReason);
                            return;
                        }
                        else
                        {
                            nextSchedule = Math.Min(nextSchedule, timeLeft);
                        }
                    }

                    if (NoDataTimeout > 0)
                    {
                        var lrt = LastReceive();

                        if (lrt >= NoDataTimeout)
                        {
                            InternalDisconnect(0, DisconnectReason.Timeout);
                            return;
                        }
                        else
                        {
                            nextSchedule = Math.Min(nextSchedule, NoDataTimeout - lrt);
                        }
                    }
                }
                break;
        }

        // safety to prevent us for scheduling ourselves for a time period that has already passed,
        // as doing so could result in infinite looping in the priority queue processing.
        // in theory this cannot happen, I should likely assert here just to make sure...
        if (nextSchedule < 0)
            nextSchedule = 0;

        // add 5ms to ensure that we are indeed slightly past the scheduled time
        UdpManager.SetPriority(this, UdpManager.CachedClock + nextSchedule + 5);
    }

    private int TotalPendingBytes()
    {
        lock (_guard)
        {
            var total = 0;

            for (var i = 0; i < Constants.ReliableChannelCount; i++)
            {
                if (Channel[i] is not null)
                    total += Channel[i].TotalPendingBytes();
            }

            return total;
        }
    }

    private void RawSend(ReadOnlySpan<byte> data, int dataLen)
    {
        // raw send resets last send time, so we need to potentially recalculate when we need time again
        // sends the actual physical packet (usually just after it has be prepped by PacketSend, but for connect/confirm/unreachable packets are bypass that step)

        UdpManager.ActualSend(data, dataLen, SocketAddress);

        ConnectionStats.TotalPacketsSent++;
        ConnectionStats.TotalBytesSent += dataLen;

        LastPortAliveTime = LastSendTime = UdpManager.CachedClock;

        // TODO
        // track data rate

        ScheduleTimeNow();
    }

    private void PhysicalSend(Span<byte> data, int dataLen, bool appendAllowed)
    {
        // if we attempt to do a physical send (ie. encrypt/compress/crc a packet) while we are not connected
        // (especially if we are cStatusNegotiating), then it will potentially crash, because in the case of
        // cStatusNegotiating, we don't have the encryption method function pointer initialized yet, as the method
        // is part of the negotiations
        if (Status != Status.Connected && Status != Status.DisconnectPending)
            return;

        var finalStart = data;
        var finalLen = dataLen;

        for (var i = 0; i < Constants.EncryptPasses; i++)
        {
            if (ConnectionConfig.EncryptMethod[i] == EncryptMethod.None)
                continue;

            var destStart = _tempEncryptBuffer[i].AsSpan();

            var destPtr = destStart;

            destPtr[0] = finalStart[0];

            if (finalStart[0] == 0)
            {
                // we know this internal packet will not be a connect or confirm packet since they are sent directly to RawSend to avoid getting encrypted

                destPtr[1] = finalStart[1];

                var len = EncryptFunction[i](destPtr.Slice(2), finalStart.Slice(2, finalLen - 2));

                // would be really odd for encryption to return an error, but if it does, throw it away
                if (len == -1)
                    return;

                finalLen = len + 2;
            }
            else
            {
                var len = EncryptFunction[i](destPtr.Slice(1), finalStart.Slice(1, finalLen - 1));

                // would be really odd for encryption to return an error, but if it does, throw it away
                if (len == -1)
                    return;

                finalLen = len + 1;
            }

            finalStart = destStart;

            appendAllowed = true;
        }

        if (ConnectionConfig.CrcBytes > 0)
        {
            if (!appendAllowed)
            {
                // if the buffer we are going to append onto was our original (ie. no encryption took place)
                // then we have to copy it all over to a temp buffer since we can't modify the original
                finalStart.Slice(0, finalLen).CopyTo(_tempEncryptBuffer[0]);
                finalStart = _tempEncryptBuffer[0];
            }

            var crc = UdpMisc.Crc32(finalStart, finalLen, ConnectionConfig.EncryptCode);

            var crcPtr = finalStart.Slice(finalLen);

            switch (ConnectionConfig.CrcBytes)
            {
                case 1:
                    crcPtr[0] = (byte)crc;
                    break;

                case 2:
                    BinaryPrimitives.WriteUInt16BigEndian(crcPtr, (ushort)crc);
                    break;

                case 3:
                    UdpMisc.PutValue24(crcPtr, crc);
                    break;

                case 4:
                    BinaryPrimitives.WriteUInt32BigEndian(crcPtr, crc);
                    break;
            }

            finalLen += ConnectionConfig.CrcBytes;
        }

        RawSend(finalStart, finalLen);
    }

    internal Span<byte> BufferedSend(Span<byte> data, int dataLen, Span<byte> data2, int dataLen2, bool appendAllowed)
    {
        var used = MultiBufferOffset;
        var ptr = MultiBufferData.AsSpan();

        var actualMaxDataHoldSize = Math.Min(UdpManager.Params.MaxDataHoldSize, ConnectionConfig.MaxRawPacketSize);

        var totalDataLen = dataLen + dataLen2;

        if (totalDataLen > 255 || (totalDataLen + 3) > actualMaxDataHoldSize)
        {
            // too long of data to even attempt a multi-buffer of this packet, so let's just send it unbuffered
            // but first, to ensure the packet-order integrity is somewhat maintained, flush the multi-buffer
            // if it currently has something in it

            if (used > 2)
                FlushMultiBuffer();

            // now send it (the multi-buffer is empty if you need to use it temporarily to concatenate two data chunks -- it is large enough to hold the largest raw packet)

            if (!data2.IsEmpty)
            {
                data.Slice(0, dataLen).CopyTo(ptr);
                data2.Slice(0, dataLen2).CopyTo(ptr.Slice(dataLen));

                PhysicalSend(ptr, totalDataLen, true);
            }
            else
            {
                PhysicalSend(data, dataLen, appendAllowed);
            }

            return null;
        }

        // if this data will not fit into buffer
        // note: we allow the multi-packet to grow as large as maxRawPacketSize, but down below we will flush it
        // as soon as it gets larger than maxDataHoldSize.
        if (used + totalDataLen + 1 > (ConnectionConfig.MaxRawPacketSize - ConnectionConfig.CrcBytes - EncryptExpansionBytes))
        {
            FlushMultiBuffer();
            used = 0;
        }

        // add data to buffer
        if (used == 0)
        {
            // no buffered data yet, create multi-packet header

            ptr[MultiBufferOffset++] = 0;
            ptr[MultiBufferOffset++] = (byte)UdpPacketType.Multi;

            // new multi-buffer started, so we need to potentially recalculate when we need time again

            // set data hold time to when the first piece of data is stuck in the multi-buffer
            DataHoldTime = UdpManager.CachedClock;

            ScheduleTimeNow();
        }

        ptr[MultiBufferOffset++] = (byte)totalDataLen;

        var placementPtr = ptr.Slice(MultiBufferOffset);

        data.Slice(0, dataLen).CopyTo(ptr.Slice(MultiBufferOffset));

        MultiBufferOffset += dataLen;

        if (!data2.IsEmpty)
        {
            data2.Slice(0, dataLen2).CopyTo(ptr.Slice(MultiBufferOffset));
            MultiBufferOffset += dataLen2;
        }

        if (MultiBufferOffset >= actualMaxDataHoldSize)
        {
            FlushMultiBuffer();
            placementPtr = null;
        }

        return placementPtr;
    }

    private void FlushMultiBuffer()
    {
        lock (_guard)
        {
            var len = MultiBufferOffset;
            var ptr = MultiBufferData.AsSpan();

            if (len > 2)
            {
                if (ptr[2] + 3 == len)
                {
                    // only one packet so don't send it as a multi-packet
                    PhysicalSend(ptr.Slice(3), len - 3, true);
                }
                else
                {
                    PhysicalSend(ptr, len, true);
                }

                // notify all the reliable channels to clear their buffered acks
                for (var i = 0; i < Constants.ReliableChannelCount; i++)
                    Channel[i]?.ClearBufferedAck();
            }

            MultiBufferOffset = 0;
        }
    }

    public void Disconnect(int flushTimeout = 0)
    {
        lock (_guard)
        {
            InternalDisconnect(flushTimeout, DisconnectReason.Application);
        }
    }

    private UdpClockStamp LastReceive(UdpClockStamp useStamp)
    {
        lock (_guard)
        {
            return UdpMisc.ClockDiff(LastReceiveTime, useStamp);
        }
    }

    private UdpClockStamp LastReceive()
    {
        lock (_guard)
        {
            return UdpManager.CachedClockElapsed(LastReceiveTime);
        }
    }

    private bool IsNonEncryptPacket(UdpPacketType packetType)
    {
        return packetType
            is UdpPacketType.Connect
            or UdpPacketType.Confirm
            or UdpPacketType.UnreachableConnection
            or UdpPacketType.RequestRemap
            or UdpPacketType.Unknown
            or UdpPacketType.ServerStatus;
    }

    private UdpClockStamp ConnectionAge()
    {
        lock (_guard)
        {
            return UdpManager.CachedClockElapsed(ConnectionCreateTime);
        }
    }

    internal void ScheduleTimeNow()
    {
        // if we are current in our GiveTime function getting time, then there is no need to reprioritize to 0 when we send a raw packet, since
        // the last thing we do in out GiveTime is do a scheduling calculation based on the last time a packet was sent.  This little check
        // prevents us from reprioritizing to 0, only to shortly thereafter be reprioritized to where we actually belong.
        if (!GettingTime)
        {
            UdpManager.SetPriority(this, 0);
        }
    }

    #region Encryption

    private int EncryptNone(Span<byte> destData, Span<byte> sourceData)
    {
        return sourceData.TryCopyTo(destData) ? sourceData.Length : -1;
    }

    private int DecryptNone(Span<byte> destData, Span<byte> sourceData)
    {
        return sourceData.TryCopyTo(destData) ? sourceData.Length : -1;
    }

    protected virtual int EncryptUserSupplied(Span<byte> destData, Span<byte> sourceData)
    {
        return sourceData.TryCopyTo(destData) ? sourceData.Length : -1;
    }

    protected virtual int DecryptUserSupplied(Span<byte> destData, Span<byte> sourceData)
    {
        return sourceData.TryCopyTo(destData) ? sourceData.Length : -1;
    }

    protected virtual int EncryptUserSupplied2(Span<byte> destData, Span<byte> sourceData)
    {
        return sourceData.TryCopyTo(destData) ? sourceData.Length : -1;
    }

    protected virtual int DecryptUserSupplied2(Span<byte> destData, Span<byte> sourceData)
    {
        return sourceData.TryCopyTo(destData) ? sourceData.Length : -1;
    }

    private int EncryptXorBuffer(Span<byte> destData, Span<byte> sourceData)
    {
        if (_encryptXorBuffer is null)
            return -1;

        var destPtr = 0;
        var sourcePtr = 0;

        var encryptPtr = 0;
        var encryptBuffer = _encryptXorBuffer.AsSpan();

        var prev = ConnectionConfig.EncryptCode;

        while (sourcePtr + sizeof(int) <= sourceData.Length)
        {
            var hold = MemoryMarshal.Read<int>(sourceData.Slice(sourcePtr));
            var encrypt = MemoryMarshal.Read<int>(encryptBuffer.Slice(encryptPtr));
            var value = hold ^ encrypt ^ prev;

            MemoryMarshal.Write(destData.Slice(destPtr), value);

            prev = value;

            destPtr += sizeof(int);
            sourcePtr += sizeof(int);
            encryptPtr += sizeof(int);
        }

        while (sourcePtr != sourceData.Length)
            destData[destPtr++] = (byte)(sourceData[sourcePtr++] ^ _encryptXorBuffer[encryptPtr++]);

        return sourceData.Length;
    }

    private int DecryptXorBuffer(Span<byte> destData, Span<byte> sourceData)
    {
        if (_encryptXorBuffer is null)
            return -1;

        var destPtr = 0;
        var sourcePtr = 0;

        var encryptPtr = 0;
        var encryptBuffer = _encryptXorBuffer.AsSpan();

        var prev = ConnectionConfig.EncryptCode;

        while (sourcePtr + sizeof(int) <= sourceData.Length)
        {
            var hold = MemoryMarshal.Read<int>(sourceData.Slice(sourcePtr));
            var encrypt = MemoryMarshal.Read<int>(encryptBuffer.Slice(encryptPtr));

            MemoryMarshal.Write(destData.Slice(destPtr), hold ^ prev ^ encrypt);

            prev = hold;

            destPtr += sizeof(int);
            sourcePtr += sizeof(int);
            encryptPtr += sizeof(int);
        }

        while (sourcePtr != sourceData.Length)
            destData[destPtr++] = (byte)(sourceData[sourcePtr++] ^ _encryptXorBuffer[encryptPtr++]);

        return sourceData.Length;
    }

    private int EncryptXor(Span<byte> destData, Span<byte> sourceData)
    {
        var destPtr = 0;
        var sourcePtr = 0;

        var prev = ConnectionConfig.EncryptCode;

        while (sourcePtr + sizeof(int) <= sourceData.Length)
        {
            var hold = MemoryMarshal.Read<int>(sourceData.Slice(sourcePtr));
            var value = hold ^ prev;

            MemoryMarshal.Write(destData.Slice(destPtr), value);

            prev = value;

            destPtr += sizeof(int);
            sourcePtr += sizeof(int);
        }

        while (sourcePtr != sourceData.Length)
            destData[destPtr++] = (byte)(sourceData[sourcePtr++] ^ prev);

        return sourceData.Length;
    }

    private int DecryptXor(Span<byte> destData, Span<byte> sourceData)
    {
        var destPtr = 0;
        var sourcePtr = 0;

        var prev = ConnectionConfig.EncryptCode;

        while (sourcePtr + sizeof(int) <= sourceData.Length)
        {
            var hold = MemoryMarshal.Read<int>(sourceData.Slice(sourcePtr));

            MemoryMarshal.Write(destData.Slice(destPtr), hold ^ prev);

            prev = hold;

            destPtr += sizeof(int);
            sourcePtr += sizeof(int);
        }

        while (sourcePtr != sourceData.Length)
            destData[destPtr++] = (byte)(sourceData[sourcePtr++] ^ prev);

        return sourceData.Length;
    }

    private void SetupEncryptModel()
    {
        EncryptExpansionBytes = 0;

        for (var j = 0; j < Constants.EncryptPasses; j++)
        {
            switch (ConnectionConfig.EncryptMethod[j])
            {
                case EncryptMethod.None:
                    DecryptFunction[j] = DecryptNone;
                    EncryptFunction[j] = EncryptNone;
                    EncryptExpansionBytes += 0;
                    break;

                case EncryptMethod.UserSupplied:
                    DecryptFunction[j] = DecryptUserSupplied;
                    EncryptFunction[j] = EncryptUserSupplied;
                    EncryptExpansionBytes += UdpManager.Params.UserSuppliedEncryptExpansionBytes;
                    break;

                case EncryptMethod.UserSupplied2:
                    DecryptFunction[j] = DecryptUserSupplied2;
                    EncryptFunction[j] = EncryptUserSupplied2;
                    EncryptExpansionBytes += UdpManager.Params.UserSuppliedEncryptExpansionBytes2;
                    break;

                case EncryptMethod.XorBuffer:
                    {
                        DecryptFunction[j] = DecryptXorBuffer;
                        EncryptFunction[j] = EncryptXorBuffer;
                        EncryptExpansionBytes += 0;

                        // set up encrypt buffer (random numbers generated based on seed)
                        if (_encryptXorBuffer is null)
                        {
                            var len = ((UdpManager.Params.MaxRawPacketSize + 1) / 4) * 4;

                            _encryptXorBuffer = new byte[len];

                            var seed = ConnectionConfig.EncryptCode;

                            for (var i = 0; i < len; i++)
                                _encryptXorBuffer[i] = (byte)UdpMisc.Random(ref seed);
                        }
                    }
                    break;

                case EncryptMethod.Xor:
                    {
                        DecryptFunction[j] = DecryptXor;
                        EncryptFunction[j] = EncryptXor;
                        EncryptExpansionBytes += 0;
                    }
                    break;
            }
        }
    }

    #endregion

    #region Handler

    public virtual void OnRoutePacket(Span<byte> data)
    {
    }

    public virtual void OnConnectComplete()
    {
    }

    public virtual void OnTerminated()
    {
    }

    public virtual void OnCrcReject(Span<byte> data)
    {
    }

    public virtual void OnPacketCorrupt(Span<byte> data, UdpCorruptionReason reason)
    {
    }

    #endregion

    public override string ToString()
    {
        return EndPoint.ToString();
    }
}