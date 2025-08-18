using System;
using System.Buffers.Binary;
using System.Diagnostics;

using Collections.Pooled;

using Sanctuary.Core.IO;
using Sanctuary.UdpLibrary.Configuration;
using Sanctuary.UdpLibrary.Enumerations;
using Sanctuary.UdpLibrary.Packets;

namespace Sanctuary.UdpLibrary.Internal;

internal class UdpReliableChannel
{
    private const int ReadyQueueSize = 1000;

    private readonly UdpReliableConfig Config;
    private readonly UdpConnection UdpConnection;

    private UdpClockStamp LastTimeStampAcknowledged;
    private UdpClockStamp TrickleLastSend;
    private UdpClockStamp NextNeedTime;
    private UdpClockStamp WindowResetTime;
    private readonly int ChannelNumber;
    private long ReliableOutgoingId;
    private long ReliableOutgoingPendingId;
    private int ReliableOutgoingBytes;
    private int LogicalBytesQueued;
    private byte[]? BigDataPtr;
    private int BigDataLen;
    private int BigDataTargetLen;
    private UdpClockStamp AveragePingTime;
    private int MaxDataBytes;
    private int FragmentNextPos;
    private PhysicalPacket[] PhysicalPackets;
    private PooledList<LogicalPacket> LogicalPacketList = new();

    private int CongestionWindowStart;
    private int CongestionWindowSize;
    private int CongestionSlowStartThreshhold;
    private int CongestionWindowMinimum;
    private bool MaxxedOutCurrentWindow;

    private long ReliableIncomingId;
    private IncomingQueueEntry[] ReliableIncoming;

    private LogicalPacket? CoalescePacket;
    private int CoalesceOffset;
    private int CoalesceCount;
    private int MaxCoalesceAttemptBytes;

    private byte[] BufferedAckPtr;

    private int StatDuplicatePacketsReceived;
    private int StatResentPacketsAccelerated;
    private int StatResentPacketsTimedOut;

    private struct PhysicalPacket
    {
        public UdpClockStamp FirstTimeStamp;
        public UdpClockStamp LastTimeStamp;
        public LogicalPacket? Parent;
        public int? DataPtr;
        public int DataLen;
    }

    private struct IncomingQueueEntry
    {
        public LogicalPacket? Packet;
        public ReliablePacketMode Mode;

        public IncomingQueueEntry()
        {
            Mode = ReliablePacketMode.Reliable;
        }
    }

    private PhysicalPacket[] ReadyQueue;

    public UdpReliableChannel(int channelNumber, UdpConnection con, UdpReliableConfig config)
    {
        UdpConnection = con;
        ChannelNumber = channelNumber;
        Config = config;
        Config.MaxOutstandingPackets = Math.Min(Config.MaxOutstandingPackets, Constants.HardMaxOutstandingPackets);

        // start out fairly high so we don't do a lot of resends on a bad connection to start out with
        AveragePingTime = 800;
        TrickleLastSend = 0;

        var fragmentSize = Config.FragmentSize;

        if (fragmentSize == 0 || fragmentSize > UdpConnection.ConnectionConfig.MaxRawPacketSize)
            fragmentSize = UdpConnection.ConnectionConfig.MaxRawPacketSize;

        MaxDataBytes = fragmentSize - Constants.UdpPacketReliableSize - UdpConnection.ConnectionConfig.CrcBytes - UdpConnection.EncryptExpansionBytes;

        // fragment size/max-raw-packet-size set too small to allow for reliable deliver
        Debug.Assert(MaxDataBytes > 0);

        if (Config.TrickleSize != 0)
            MaxDataBytes = Math.Min(MaxDataBytes, Config.TrickleSize);

        MaxCoalesceAttemptBytes = -1;

        if (Config.Coalesce)
            MaxCoalesceAttemptBytes = MaxDataBytes - 5; // 2 bytes for group-header, 3 bytes for length of packet

        ReliableIncomingId = 0;
        ReliableOutgoingId = 0;
        ReliableOutgoingPendingId = 0;
        ReliableOutgoingBytes = 0;
        LogicalBytesQueued = 0;

        CoalescePacket = null;
        CoalesceOffset = 0;
        CoalesceCount = 0;

        BufferedAckPtr = Array.Empty<byte>();

        StatDuplicatePacketsReceived = 0;
        StatResentPacketsAccelerated = 0;
        StatResentPacketsTimedOut = 0;

        WindowResetTime = 0;

        CongestionWindowMinimum = Math.Max(MaxDataBytes, Config.CongestionWindowMinimum);
        CongestionWindowStart = Math.Min(4 * MaxDataBytes, Math.Max(2 * MaxDataBytes, 4380));
        CongestionWindowStart = Math.Max(CongestionWindowStart, Config.CongestionWindowMaximum);
        CongestionSlowStartThreshhold = Math.Min(Config.MaxOutstandingPackets * MaxDataBytes, Config.MaxOutstandingBytes);
        CongestionWindowSize = CongestionWindowStart;

        BigDataLen = 0;
        BigDataTargetLen = 0;
        BigDataPtr = null;
        FragmentNextPos = 0;
        LastTimeStampAcknowledged = 0;
        MaxxedOutCurrentWindow = false;
        NextNeedTime = 0;

        PhysicalPackets = new PhysicalPacket[Config.MaxOutstandingPackets];
        ReliableIncoming = new IncomingQueueEntry[Config.MaxInstandingPackets];

        ReadyQueue = new PhysicalPacket[ReadyQueueSize];
    }

    public int TotalPendingBytes()
    {
        return LogicalBytesQueued + ReliableOutgoingBytes;
    }

    public void ReliablePacket(Span<byte> data)
    {
        if (data.Length <= Constants.UdpPacketReliableSize)
        {
            UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.ReliablePacketTooShort);
            return;
        }

        var packetType = data[1];

        var reliableStamp = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2));

        var reliableId = GetReliableIncomingId(reliableStamp);

        // if we do not have buffer space to hold onto this packet, then we simply must pretend like it was lost
        if (reliableId >= ReliableIncomingId + Config.MaxInstandingPackets)
            return;

        if (reliableId >= ReliableIncomingId)
        {
            var mode = (ReliablePacketMode)((packetType - (byte)UdpPacketType.Reliable1) / Constants.ReliableChannelCount);

            // is this the packet we are waiting for

            if (ReliableIncomingId == reliableId)
            {
                // if so, process it immediately
                ProcessPacket(mode, data.Slice(Constants.UdpPacketReliableSize));

                ReliableIncomingId++;

                // process other packets that have arrived

                ref var packet = ref ReliableIncoming[ReliableIncomingId % Config.MaxInstandingPackets].Packet;

                while (packet is not null)
                {
                    var spot = (int)(ReliableIncomingId % Config.MaxInstandingPackets);

                    if (ReliableIncoming[spot].Mode != ReliablePacketMode.Delivered)
                        ProcessPacket(ReliableIncoming[spot].Mode, packet.GetDataPtr().Slice(0, packet.GetDataLen()));

                    packet = null;
                    ReliableIncomingId++;

                    packet = ref ReliableIncoming[ReliableIncomingId % Config.MaxInstandingPackets].Packet;
                }
            }
            else
            {
                // not the one we need next, but it is later than the one we need , so store it in our buffer until it's turn comes up

                var spot = (int)(reliableId % Config.MaxInstandingPackets);

                ref var packet = ref ReliableIncoming[spot].Packet;

                // only make the copy of it if we don't already have it in our buffer (in cases where it was sent twice, there would be no harm in the copy again since it must be the same packet, it's just inefficient)
                if (packet is null)
                {
                    ReliableIncoming[spot].Mode = mode;

                    packet = UdpConnection.UdpManager.CreatePacket(data.Slice(Constants.UdpPacketReliableSize), data.Length - Constants.UdpPacketReliableSize);

                    ReliableIncoming[spot].Packet = packet;

                    // on out of order deliver, we need to keep a copy of it as if we were doing ordered-delivery in order to prevent duplicates
                    // we will mark the packet in the queue as already delivered to prevent it from getting delivered a second time when the stalled packet
                    // arrives and unwinds the queue
                    if (mode == ReliablePacketMode.Reliable && Config.OutOfOrder)
                    {
                        ProcessPacket(ReliablePacketMode.Reliable, packet.GetDataPtr().Slice(0, packet.GetDataLen()));
                        ReliableIncoming[spot].Mode = ReliablePacketMode.Delivered;
                    }
                }
            }
        }
        else
        {
            StatDuplicatePacketsReceived++;
            UdpConnection.ConnectionStats.DuplicatePacketsReceived++;
            UdpConnection.UdpManager.IncrementDuplicatePacketsReceived();
        }

        var ackAll = false;

        var buf = new byte[4];

        var bufPtr = buf.AsSpan();

        bufPtr[0] = 0;

        if (ReliableIncomingId > reliableId)
        {
            // ack everything up to the current head of our chain (minus one since the stamp represents the next one we want to get)
            bufPtr[1] = (byte)((byte)UdpPacketType.AckAll1 + ChannelNumber);
            BinaryPrimitives.WriteUInt16BigEndian(bufPtr.Slice(2), (ushort)(ReliableIncomingId - 1));
            ackAll = true;
        }
        else
        {
            // a simple ack for us only
            bufPtr[1] = (byte)((byte)UdpPacketType.Ack1 + ChannelNumber);
            BinaryPrimitives.WriteUInt16BigEndian(bufPtr.Slice(2), (ushort)reliableId);
        }

        var bufferedAckPtr = BufferedAckPtr.AsSpan();

        if (!bufferedAckPtr.IsEmpty && Config.AckDeduping && ackAll)
        {
            bufPtr.CopyTo(BufferedAckPtr);
        }
        else
        {
            // safe to append on our data, it is stack data
            var ptr = UdpConnection.BufferedSend(bufPtr, bufPtr.Length, null, 0, true);

            if (bufferedAckPtr.IsEmpty)
            {
                // the buffered-ack ptr should always point to the earliest ack in the buffer, such that
                // a replacement ack-all will be processed by the receiver before any selective acks that may
                // have been buffered up.  Thus, only repoint the buffered ack ptr if it was previously unset.
                bufferedAckPtr = ptr;
            }
        }
    }

    public void Send(Span<byte> data, int dataLen, Span<byte> data2, int dataLen2)
    {
        if (LogicalPacketList.Count == 0 && CoalescePacket is null)
        {
            // if we are adding something to a previously empty logical queue, then it is possible that
            // we may be able to send it, so mark ourselves to take time the next time it is offered
            NextNeedTime = 0;
            UdpConnection.ScheduleTimeNow();
        }

        if (dataLen + dataLen2 <= MaxCoalesceAttemptBytes)
        {
            SendCoalesce(data, dataLen, data2, dataLen2);
        }
        else
        {
            FlushCoalesce();

            var packet = UdpConnection.UdpManager.CreatePacket(data, dataLen, data2, dataLen2);

            QueueLogicalPacket(packet);
        }
    }

    public void AckPacket(Span<byte> data)
    {
        if (data.Length < 4)
        {
            UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.AckBad);
            return;
        }

        var reader = new PacketReader(data.Slice(2));

        if (!reader.TryReadUInt16(out var reliableStamp))
        {
            UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.AckBad);
            return;
        }

        Ack(GetReliableOutgoingId(reliableStamp));
    }

    private long GetReliableOutgoingId(int reliableStamp)
    {
        // since we can never have anywhere close to 65000 packets outstanding, we only need to
        // to send the low order word of the reliableId in the UdpPacketReliable and UdpPacketAck
        // packets, because we can reconstruct the full id from that, we just need to take
        // into account the wrap around issue.  We calculate it based of the high-word of the
        // next packet we are going to send.  If it ends up being larger then we know
        // we wrapped and can fix it up by simply subtracting 1 from the high-order word.

        var reliableId = (long)reliableStamp | ReliableOutgoingId & ~0xffffL;

        if (reliableId > ReliableOutgoingId)
            reliableId -= 0x10000;

        return reliableId;
    }

    public void AckAllPacket(Span<byte> data)
    {
        if (data.Length < 4)
        {
            UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.AckBad);
            return;
        }

        var reader = new PacketReader(data.Slice(2));

        if (!reader.TryReadUInt16(out ushort reliableStamp))
        {
            UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.AckBad);
            return;
        }

        var reliableId = GetReliableOutgoingId(reliableStamp);

        if (ReliableOutgoingPendingId > reliableId)
        {
            // if we ackall'ed a packet and everything before the ackall address had already been acked, then we know
            // for certainty that we sent a packet over again that did not need to be sent over again (ie. wasn't lost, just slow)
            // so adjust the mAveragePingTime upward to slow down future resends
            AveragePingTime += 400;
            AveragePingTime = Math.Min(Config.ResendDelayCap, AveragePingTime);
        }

        for (var i = ReliableOutgoingPendingId; i <= reliableId; i++)
            Ack(i);
    }

    public void ClearBufferedAck()
    {
        BufferedAckPtr = Array.Empty<byte>();
    }

    public UdpClockStamp GiveTime()
    {
        var hotClock = UdpConnection.UdpManager.CachedClock;

        if (hotClock < NextNeedTime)
            return UdpMisc.ClockDiff(hotClock, NextNeedTime);

        // if we are a trickle channel, then don't try sending more until trickleRate has expired.  We are only allowed
        // to send up to trickleBytes at a time every trickleRate milliseconds; however, if we don't send the full trickleBytes
        // in one GiveTime call, then it won't get to try sending more bytes until this timer has expired, even if we had not used
        // up the entire trickleBytes allotment the last time we were in here...this should not cause any significant problems

        if (Config.TrickleRate > 0)
        {
            var nextAllowedSendTime = Config.TrickleRate - UdpMisc.ClockDiff(TrickleLastSend, hotClock);

            if (nextAllowedSendTime > 0)
                return nextAllowedSendTime;
        }

        // lot a tweaking goes into calculating the optimal resend time.  Set it too large and you can stall the pipe
        // at the beginning of the connection fairly easily

        // percentage of average ping plus a fixed amount
        var optimalResendDelay = AveragePingTime * Config.ResendDelayPercent / 100 + Config.ResendDelayAdjust;

        // never let the resend delay get over max
        optimalResendDelay = Math.Min(Config.ResendDelayCap, optimalResendDelay);

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // see if any of the physical packets can actually be sent (either resends, or initial sends, whatever
        // if not, calculate when exactly somebody is expected to need sending
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        MaxxedOutCurrentWindow = false;

        var outstandingNextSendTime = 10 * 60000L;

        // if we have something to do
        if (ReliableOutgoingPendingId < ReliableOutgoingId || LogicalPacketList.Count != 0 || CoalescePacket is not null)
        {
            // first, let's calculate how many bytes we figure is outstanding based on who is still waiting for an ack-packet
            // anything older than this, we need to resend
            var oldestResendTime = Math.Max(hotClock - optimalResendDelay, LastTimeStampAcknowledged);

            bool readyQueueOverflow;

            Span<byte> buf = stackalloc byte[8];

            do
            {
                readyQueueOverflow = false;

                var useMaxOutstandingBytes = Math.Min(Config.MaxOutstandingBytes, CongestionWindowSize);

                var outstandingBytes = 0;

                var readyEnd = ReadyQueue.Length;
                var readyPtr = 0;

                var windowSpaceLeft = useMaxOutstandingBytes;

                for (var i = ReliableOutgoingPendingId; i <= ReliableOutgoingId; i++)
                {
                    if (i == ReliableOutgoingId)
                    {
                        // this packet is not really here yet, we need to pull it down if possible
                        // if not possible, we need to break out of the loop as we are done
                        if (!PullDown(windowSpaceLeft))
                            break;
                    }

                    // if this packet has not been acked and it is NOT ready to be sent (was recently sent) then we consider it outstanding
                    // note: packets needing re-sending probably got lost and are therefore not outstanding
                    ref var entry = ref PhysicalPackets[i % Config.MaxOutstandingPackets];

                    // acked packets set the dataPtr to nullptr
                    if (entry.DataPtr.HasValue)
                    {
                        // if this packet is ready to be sent (ie: needs time now, or some later packet has already been ack'ed)

                        // window-space is effectively taken whether we have sent it yet or not
                        windowSpaceLeft -= entry.DataLen;

                        if (entry.LastTimeStamp < oldestResendTime)
                        {
                            // if we have queue space
                            if (readyPtr < readyEnd)
                            {
                                ReadyQueue[readyPtr++] = entry;
                            }
                            else
                            {
                                // we ran out of space in the ready-queue, so we are going to have to do this whole process
                                // over again.  Normally we would just want to make sure the readyQueue was really big to save
                                // us doing all this stuff twice; however, that uses up a ton of stack space we may not have
                                // and really only happens in situations where there is a ton of stuff waiting to be sent
                                // (ie, super high bandwidth situations really).  So I don't mind taking a performance hit
                                // for those situations to keep the stack size small for the vast majority of normal situations.
                                // note: this was initially added in support of the tiny stack the PS3 had.
                                readyQueueOverflow = true;
                            }
                        }
                        else
                        {
                            outstandingBytes += entry.DataLen;
                            outstandingNextSendTime = Math.Min(outstandingNextSendTime, optimalResendDelay - UdpMisc.ClockDiff(entry.LastTimeStamp, hotClock));
                        }

                        // if we have reached a point in the queue where there are no sent packets
                        // and our outstanding bytes plus how much we intend to send is greater than the window we have
                        // then we can quit step-2, since there is nothing else to be gained by continuing.
                        if (entry.FirstTimeStamp == 0 && windowSpaceLeft <= 0)
                            break;
                    }
                }

                // second, send ready entries until the max outstanding is reached
                var toleranceLossCount = 0;

                // only allow it to reset the window again if it has been longer than the average ping time
                var allowWindowReset = UdpMisc.ClockDiff(WindowResetTime, hotClock) > AveragePingTime;
                var trickleSent = 0;

                var readyWalk = 0;
                while (readyWalk < readyPtr && outstandingBytes < useMaxOutstandingBytes)
                {
                    // prepare packet and send it
                    ref var entry = ref ReadyQueue[readyWalk++];

                    ArgumentNullException.ThrowIfNull(entry.Parent);
                    ArgumentNullException.ThrowIfNull(entry.DataPtr);

                    // prepare reliable header and send it with data
                    var parentBase = entry.Parent.GetDataPtr();

                    var fragment = false;

                    if (entry.DataPtr.Value != 0 || entry.DataLen != entry.Parent.GetDataLen())
                        fragment = true;

                    // we can calculate what our reliableId should be based on our position in the array
                    // need to handle the case where we wrap around the end of the array
                    var reliableId = ReliableOutgoingPendingId + (readyWalk - 1);

                    // prep the actual packet and send it
                    buf[0] = 0;

                    // mark us as a fragment if we are one
                    buf[1] = (byte)((fragment ? (byte)UdpPacketType.Fragment1 : (byte)UdpPacketType.Reliable1) + ChannelNumber);

                    BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(2), (ushort)reliableId);

                    // first fragment has a total-length byte after the reliable header
                    if (fragment && entry.DataPtr.Value == 0)
                    {
                        BinaryPrimitives.WriteInt32BigEndian(buf.Slice(4), entry.Parent.GetDataLen());
                        UdpConnection.BufferedSend(buf, 8, parentBase.Slice(entry.DataPtr.Value), entry.DataLen, false);
                    }
                    else
                    {
                        UdpConnection.BufferedSend(buf, 4, parentBase.Slice(entry.DataPtr.Value), entry.DataLen, false);
                    }

                    // update state information
                    if (entry.FirstTimeStamp == 0)
                    {
                        entry.FirstTimeStamp = hotClock;
                    }
                    else
                    {
                        // trying to send the packet again, let's see how long we have been trying to send this packet.  If we
                        // have an unacknowledged timeout set and it is older than that, then terminate the connection.
                        // note: we only check for the oldest unacknowledged age against the timeout at the point in time
                        // that we are considering sending the packet again.  This can technically cause it to wait slightly
                        // longer than the specified timeout setting before disconnecting the connection, but should be
                        // close enough for all practical purposes and allows for more efficient processing of this setting internally.
                        if (UdpConnection.UdpManager.Params.OldestUnacknowledgedTimeout > 0)
                        {
                            var age = UdpMisc.ClockDiff(entry.FirstTimeStamp, hotClock);
                            if (age > UdpConnection.UdpManager.Params.OldestUnacknowledgedTimeout)
                            {
                                UdpConnection.InternalDisconnect(0, DisconnectReason.UnacknowledgedTimeout);
                                return 0;
                            }
                        }

                        // were we resent because of a later ack came in? or because we timed out?
                        if (entry.LastTimeStamp < LastTimeStampAcknowledged)
                        {
                            // we are resending this packet due to an accelleration (receiving a later packet ack)
                            // so recalc slow start threshhold and congestion window size as per Reno fast-recovery algorithm
                            if (allowWindowReset && toleranceLossCount > Config.ToleranceLossCount)
                            {
                                allowWindowReset = false;
                                WindowResetTime = hotClock;
                                CongestionWindowSize = CongestionWindowSize * 3 / 4;
                                // never let congestion window get smaller than a single packet
                                CongestionWindowSize = Math.Max(CongestionWindowMinimum, CongestionWindowSize);
                                CongestionSlowStartThreshhold = CongestionWindowSize;
                                useMaxOutstandingBytes = Math.Min(Config.MaxOutstandingBytes, CongestionWindowSize);
                            }

                            // when resends are caused by a selective ack, that means later data is making it, so we may not be overloading
                            // the connection after all.  Reno fast recovery calls for less shrinking of the window in this circumstance
                            // already.  This tolerance code allows us to do even better, by allowing us to experience some small amount
                            // of packetloss without immediately considering such loss to be indicative that the connection is being overloaded.
                            // It does this by only allowing the window to get reset in cases where teh selective-ack forces a certain number
                            // of packets to be accellerated.  If the application sets the tolerance at something like 10, then isolated packet
                            // losses will have no effect on the flow control window, yet a burst of loss of 10 would more strongly indicate
                            // a overloaded condition.  Typically an application will only set this tolerance if they need extremely high
                            // bandwidth (ie, can't afford the window getting reset), and are known to be on a connection that may experience
                            // random packetloss.  This is typical of a LFN type network.
                            toleranceLossCount++;

                            StatResentPacketsAccelerated++;
                            UdpConnection.ConnectionStats.ResentPacketsAccelerated++;
                            UdpConnection.UdpManager.IncrementResentPacketsAccelerated();
                        }
                        else
                        {
                            // we are resending this packet due to a timeout, so we are seriously overloading things probably
                            // so recalc slow start threshhold and congestion window size as per Reno algorithm
                            // no tolerance on timed-out window resets, since by definition you have not received later acks
                            // or this would be an accellerated timeout, which means that either your volume is really low
                            // anyways (so resetting the window is fine), or you seriously overloaded the other side as nothing
                            // later made it either.
                            if (allowWindowReset)
                            {
                                allowWindowReset = false;
                                WindowResetTime = hotClock;

                                CongestionSlowStartThreshhold = Math.Max(MaxDataBytes * 2, CongestionWindowSize / 2);
                                CongestionWindowSize = CongestionWindowStart;
                                useMaxOutstandingBytes = Math.Min(Config.MaxOutstandingBytes, CongestionWindowSize);

                                // because a resend has occurred due to a timeout, slow down the resend times slightly
                                // when things start flowing again, it will fix itself up quickly anyways
                                AveragePingTime += 100;

                                // When a connection goes temporarily dead, everything that is in the current
                                // window will end up getting timedout.  If the window were large, this could result in the
                                // mAveragePingTime growing quite large and creating very long stalls in the pipe once it does
                                // start moving again.  To prevent this, we cap mAveragePingTime when these events occur to prevent
                                // long stalls when the pipe finally reopens.
                                AveragePingTime = Math.Min(Config.ResendDelayCap, AveragePingTime);
                            }

                            StatResentPacketsTimedOut++;
                            UdpConnection.ConnectionStats.ResentPacketsTimedOut++;
                            UdpConnection.UdpManager.IncrementResentPacketsTimedOut();
                        }
                    }

                    entry.LastTimeStamp = hotClock;

                    // this packet is now outstanding, so factor it into the outstandingNextSendTime calculation
                    outstandingNextSendTime = Math.Min(outstandingNextSendTime, optimalResendDelay);

                    outstandingBytes += entry.DataLen;
                    TrickleLastSend = hotClock;
                    trickleSent += entry.DataLen;

                    if (Config.TrickleSize != 0 && trickleSent >= Config.TrickleSize)
                        break;
                }

                if (outstandingBytes >= useMaxOutstandingBytes)
                {
                    MaxxedOutCurrentWindow = true;
                }

                // if we filled up the ready queue (ie, if we may still have data to send)
                // and we still have space left in the flow window, then repeat the process.
                // this should only happen when the flow-control window is enormous.
            } while (readyQueueOverflow && !MaxxedOutCurrentWindow);
        }
        else
        {
            // we have nothing in the pipe at all, reset the congestion window (this means everything has been acked, so the pipe is totally empty
            // we need to reset the window back to prevent a sudden flood next time a large chunk of data is sent.
            // we also need to avoid having the slowly sent reliable packets constantly increase the window size (since none will ever get lost)
            // such that when it does come time to send a big chunk of data, it thinks the window-size is enormous.
            // resetting the window back to small will only have an effect if a large chunk of data is then sent, at which time it will quickly
            // ramp up with the slow-start method.
            CongestionWindowSize = CongestionWindowStart;
        }

        {
            var nextAllowedSendTime = Config.TrickleRate - UdpMisc.ClockDiff(TrickleLastSend, hotClock);

            nextAllowedSendTime = Math.Max(0, Math.Max(nextAllowedSendTime, outstandingNextSendTime));

            NextNeedTime = hotClock + nextAllowedSendTime;

            return nextAllowedSendTime;
        }
    }

    private long GetReliableIncomingId(int reliableStamp)
    {
        // since we can never have anywhere close to 65000 packets outstanding, we only need to
        // to send the low order word of the reliableId in the UdpPacketReliable and UdpPacketAck
        // packets, because we can reconstruct the full id from that, we just need to take
        // into account the wrap around issue.  We basically prepend the last-known
        // high-order word.  If we end up significantly below the head of our chain, then we
        // know we need to pick the entry 0x10000 higher.  If we fall significantly above
        // our previous high-end, then we know we need to go the other way.

        var reliableId = (long)reliableStamp | ReliableIncomingId & ~0xffffL;

        if (reliableId < ReliableIncomingId - Constants.HardMaxOutstandingPackets)
            reliableId += 0x10000;

        if (reliableId > ReliableIncomingId + Constants.HardMaxOutstandingPackets)
            reliableId -= 0x10000;

        return reliableId;
    }

    private void Ack(long reliableId)
    {
        // if packet being acknowledged is possibly in our resend queue, then check for it
        if (reliableId >= ReliableOutgoingPendingId && reliableId < ReliableOutgoingId)
        {
            var pos = (int)(reliableId % Config.MaxOutstandingPackets);
            ref var entry = ref PhysicalPackets[pos];

            // if this packet has not been acknowledged yet (sometimes we get back two acks for the same packet)
            if (entry.DataPtr.HasValue)
            {
                // something got acked, so we actually need to take the time next time it is offered
                NextNeedTime = 0;

                // if the last time we gave this reliable channel processing time, it filled up the entire sliding window
                // then go ahead and increase the window-size when incoming acks come in.  However, if the window wasn't full
                // then don't increase the window size.  The problem is, a game application is likely to send reliable data
                // at a relatively slow rate (2k/second for example), never filling the window.  The net result would be that
                // every acknowledged packet would increase the window size, giving the reliable channel the impression that
                // it's window can be very very large, when in fact, it is only not losing packets because the application is pacing
                // itself.  The window could grow enormous, even 200k for a modem.  Then, if the application were to dump a load
                // of data onto us all at once, it would flush it all out at once thinking it had a big window.  By only increasing 
                // the window size when we have high enough volume to fill the window, we ensure this does not happen.  TCP does a similar
                // thing, but what they do is reset the window if there has been a long stall.  We do that too, but because we are a game
                // application that is likely to pace the data at the application level, we have a unique circumstances that need addressing.

                if (MaxxedOutCurrentWindow)
                {
                    if (CongestionWindowSize < CongestionSlowStartThreshhold)
                    {
                        // slow-start mode
                        CongestionWindowSize += MaxDataBytes;
                    }
                    else
                    {
                        var increase = MaxDataBytes * MaxDataBytes / CongestionWindowSize;

                        // congestion mode
                        CongestionWindowSize += Math.Max(1, increase);
                    }
                }

                if (entry.LastTimeStamp == entry.FirstTimeStamp)
                {
                    // if the packet that is being acknowledged was only sent once, then we can safely use
                    // the round-trip time as an accurate measure of ping time.  By knowing ping time, we can
                    // better (more agressively) schedule resends of lost packets.  We will use a moving average
                    // that weights the current packet as 1/4 the average.
                    var thisPingTime = UdpConnection.UdpManager.CachedClockElapsed(entry.FirstTimeStamp);
                    AveragePingTime = (AveragePingTime * 3 + thisPingTime) / 4;
                }

                // what this is doing is if we receive an ACK for a packet that was sent at TIME_X
                // we can assume that all packets sent before TIME_X that are not yet acknowledge were lost
                // and we can resend them immediately
                // since we do not know whether this ack is for last packet we sent, we have to assume it is for the first time this packet was sent (if sent multiple times)
                // otherwise, we could resend it, then receive the ack from the first packet, and think our last-ack time is the time of the second outgoing packet
                // which would cause about every packet in the queue to resend, even if they had just been sent
                // in situations where the first packet truely was lost and this is an ACK of the second packet, then the only
                // harm done is that the we may not resend some of the earlier sent packets quite as quickly.  This will only
                // happen in situations where a packet that was truely lost gets acked on it's second attempt...we just
                // won't be using that ack for the purposes of accelerating other resends...since odds are a non-lost packet
                // will accelerate those other resends shortly anyhow, there really is no loss
                // (note: we used to only set this value forward for packets that were never lost (one time sends); however, if this stamp
                // ever got set way high for some reason (in theory it can't happen), then we would get into a situation where it would
                // rapidly resend and possibly never get reset, causing infinite rapid resends, so we now set it every time to the first-stamp)
                // which will be safe, even if the packet were resent.)
                LastTimeStampAcknowledged = entry.FirstTimeStamp;

                // this packet we have queued has been acknowledged, so delete it from queue
                ReliableOutgoingBytes -= entry.DataLen;
                entry.DataLen = 0;
                entry.DataPtr = null;
                entry.Parent = null;

                // advance the pending ptr until it reaches outgoingId or an entry that has yet to acknowledged
                while (ReliableOutgoingPendingId < ReliableOutgoingId)
                {
                    if (PhysicalPackets[ReliableOutgoingPendingId % Config.MaxOutstandingPackets].DataPtr.HasValue)
                        break;

                    ReliableOutgoingPendingId++;
                }
            }
            else
            {
                // we got an ack for a packet that has already been acked.  This could be due to an ack-all packet that covered us so statistically
                // we can't do much with this information.
            }
        }

        // we don't need to try rescheduling ourself here, since our connection object reschedules to go immediately whenever any type
        // of packet arrives (including ack packets)
    }

    private void ProcessPacket(ReliablePacketMode mode, Span<byte> data)
    {
        var reader = new PacketReader(data);

        // if we have a big packet under construction already, or we are a fragment and thus need to be constructing one, then append this on the end (will create new if it is the first fragment)
        if (mode == ReliablePacketMode.Reliable)
        {
            // we are not a fragment, nor was there a fragment in progress, so we are a simple reliable packet, just send it to the app
            if (BigDataPtr is not null)
            {
                UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.FragmentExpected);
                return;
            }

            UdpConnection.ProcessCookedPacket(data);
        }
        else if (mode == ReliablePacketMode.Fragment)
        {
            // append onto end of big packet (or create new big packet if not existing already)
            if (BigDataPtr is null)
            {
                if (data.Length < 4)
                {
                    UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.FragmentBad);
                    return;
                }

                // first fragment has a total-length int header on it.
                if (!reader.TryReadInt32(out BigDataTargetLen))
                    return;

                // negative or zero sized chunk to follow means packet corruption or tampering for sure
                if (BigDataTargetLen <= 0)
                {
                    UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.FragmentBad);
                    return;
                }

                if (BigDataTargetLen > UdpConnection.UdpManager.Params.IncomingLogicalPacketMax)
                {
                    UdpConnection.CallbackCorruptPacket(data, UdpCorruptionReason.FragmentOversized);
                    return;
                }

                BigDataPtr = new byte[BigDataTargetLen];
                BigDataLen = 0;
            }

            // can't happen in theory since they should add up exact, but protect against it if it does
            var safetyMax = Math.Min(BigDataTargetLen - BigDataLen, reader.RemainingLength);

            Debug.Assert(safetyMax == reader.RemainingLength);

            reader.RemainingSpan.CopyTo(BigDataPtr.AsSpan(BigDataLen));
            BigDataLen += safetyMax;

            if (BigDataTargetLen == BigDataLen)
            {
                // send big-packet off to application
                UdpConnection.ProcessCookedPacket(BigDataPtr.AsSpan(0, BigDataLen));

                // delete big packet, and reset
                BigDataLen = 0;
                BigDataTargetLen = 0;
                BigDataPtr = null;
            }
        }
    }

    private bool PullDown(int windowSpaceLeft)
    {
        // the pull-down on-demand method will give us the opportunity to late-combine as many tiny logical packets as we can,
        // reducing the number of tracked-packets that are required.  This effectively reduces the number of acks that we are
        // going to get back, which can be substantial in situations where there are a LOT of tiny reliable packets being sent.
        // operating with fewer outstanding physical-packets to track is more CPU efficient as well.

        // (NOTE: as of this writing, the below implementation does not do the late-combine techique yet)
        // (NOTE: we currently combine on send instead, which generally accomplishes the same thing)

        var pulledDown = false;
        var physicalCount = ReliableOutgoingId - ReliableOutgoingPendingId;

        while (windowSpaceLeft > 0 && physicalCount < Config.MaxOutstandingPackets)
        {
            if (LogicalPacketList.Count == 0)
            {
                // this is guaranteed to stick
                FlushCoalesce();

                // nothing flushed, so we are done
                if (LogicalPacketList.Count == 0)
                    break;
            }

            var nextSpot = ReliableOutgoingId % Config.MaxOutstandingPackets;

            // ok, we can move something down, even if it is only a fragment of the logical packet
            ref var entry = ref PhysicalPackets[nextSpot];
            entry.Parent = LogicalPacketList[0];
            entry.FirstTimeStamp = 0;
            entry.LastTimeStamp = 0;

            // calculate how much we can send based on our starting position (mFragmentNextPos) in the logical packet.
            // if we can't send it the rest of data to end of packet, then send the fragment portion and addref, otherwise send the whole thing and pop the logical packet
            var dataLen = entry.Parent.GetDataLen();

            var bytesLeft = dataLen - FragmentNextPos;
            var bytesToSend = Math.Min(bytesLeft, MaxDataBytes);

            entry.DataPtr = FragmentNextPos;

            // if not sending entire packet
            if (bytesToSend != dataLen)
            {
                // mark it as a fragment
                // fragment start has a 4 byte header specifying size of following large data, so make room for it so we don't exceed max raw packet size
                if (FragmentNextPos == 0)
                    bytesToSend -= 4;
            }

            entry.DataLen = bytesToSend;
            ReliableOutgoingBytes += bytesToSend;

            if (bytesToSend == bytesLeft)
            {
                FragmentNextPos = 0;
                LogicalPacketList.Remove(entry.Parent);
            }
            else
            {
                FragmentNextPos += bytesToSend;
            }

            // as fragments are sent, decrease the number of logical bytes queued
            LogicalBytesQueued -= bytesToSend;

            ReliableOutgoingId++;

            physicalCount++;

            windowSpaceLeft -= bytesToSend;

            pulledDown = true;
        }

        return pulledDown;
    }

    private void FlushCoalesce()
    {
        if (CoalescePacket is null)
            return;

        if (CoalesceCount == 1)
        {
            var dataPtr = CoalescePacket.GetDataPtr();

            // only one packet in coalesce, so remove the coalesce/group header and make it just a simple raw packet

            var skipLen = UdpMisc.GetVariableValue(dataPtr.Slice(2), out var firstLen);
            dataPtr.Slice(2 + skipLen, firstLen).CopyTo(dataPtr);

            CoalesceOffset = firstLen;
        }

        CoalescePacket.SetDataLen(CoalesceOffset);
        QueueLogicalPacket(CoalescePacket);
        CoalescePacket = null;
    }

    private void SendCoalesce(Span<byte> data, int dataLen, Span<byte> data2 = default, int dataLen2 = 0)
    {
        var totalLen = dataLen + dataLen2;

        if (CoalescePacket is null)
        {
            CoalescePacket = UdpConnection.UdpManager.CreatePacket(null, MaxDataBytes);

            CoalesceOffset = 0;

            var dataPtr = CoalescePacket.GetDataPtr();

            dataPtr[CoalesceOffset++] = 0;
            dataPtr[CoalesceOffset++] = (byte)UdpPacketType.Group;

            CoalesceCount = 0;
        }
        else
        {
            var spaceLeft = MaxDataBytes - CoalesceOffset;

            // 3 bytes to ensure PutVariableValue has room for the length indicator (this limits us to 64k coalescing, which is ok, since fragments can't get that big)
            if (totalLen + 3 > spaceLeft)
            {
                FlushCoalesce();
                SendCoalesce(data, dataLen, data2, dataLen2);
                return;
            }
        }

        // append on end of coalesce
        CoalesceCount++;

        {
            var dataPtr = CoalescePacket.GetDataPtr();

            CoalesceOffset += UdpMisc.PutVariableValue(dataPtr.Slice(CoalesceOffset), totalLen);

            if (!data.IsEmpty)
                data.Slice(0, dataLen).CopyTo(dataPtr.Slice(CoalesceOffset));

            CoalesceOffset += dataLen;

            if (!data2.IsEmpty)
                data2.Slice(0, dataLen2).CopyTo(dataPtr.Slice(CoalesceOffset));

            CoalesceOffset += dataLen2;
        }
    }

    private void QueueLogicalPacket(LogicalPacket packet)
    {
        LogicalBytesQueued += packet.GetDataLen();
        LogicalPacketList.Add(packet);
    }

    public UdpClockStamp GetAveragePing()
    {
        return AveragePingTime;
    }
}