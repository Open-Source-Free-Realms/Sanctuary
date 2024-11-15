using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;

using Collections.Pooled;

using Sanctuary.Core.IO;
using Sanctuary.UdpLibrary.Internal;
using Sanctuary.UdpLibrary.Abstractions;
using Sanctuary.UdpLibrary.Enumerations;
using Sanctuary.UdpLibrary.Packets;
using Sanctuary.UdpLibrary.Statistics;
using Sanctuary.UdpLibrary.Configuration;

namespace Sanctuary.UdpLibrary;

/// <summary>
/// The purpose of the UdpManager is to manage a set of connections that are coming in on a particular port.
/// Typically an application will only have one UdpManager taking care of all incoming connections.  The
/// exception is if the application is talking to two distinct sets of individuals.  For example, the leaf
/// server application might have a UdpManager to manage the connections to all the users/players who will
/// be connecting.  It may then have a second UdpManager to manage its connection to a master server
/// someplace (though in theory it could use one UdpManager for everything).
///
/// The UdpManager owns the solitary socket that all data being sent/received by any of the managed connections uses.
/// When the UdpManager is created, it is given a port-number that it uses for this purpose.  The UdpManager is capable
/// of establishing new connections to other UdpManager, or it is also capable of accepting new connections.
/// </summary>
public class UdpManager<TConnection> : IUdpManager, IDisposable where TConnection : UdpConnection
{
    private readonly Lock _clockGuard = new();
    // private readonly Lock _poolGuard = new();
    private readonly Lock _eventListGuard = new();
    private readonly Lock _availableEventGuard = new();
    private readonly Lock _statsGuard = new();
    private readonly Lock _disconnectPendingGuard = new();
    // private readonly Lock _simulateGuard = new();
    private readonly Lock _giveTimeGuard = new();
    private readonly Lock _connectionGuard = new();
    private readonly Lock _handlerGuard = new();

    public UdpClockStamp CachedClock { get; private set; }

    private UdpClockStamp LastReceiveTime;
    private UdpClockStamp LastSendTime;

    public UdpParams Params { get; set; }

    public readonly IServiceProvider _serviceProvider;

    private readonly IUdpDriver _driver;

    protected UdpClockStamp LastEmptySocketBufferStamp;

    // how long the currently being processed packet could have possibly been sitting in the socket-buffer waiting for processing (which is effectively how long it has been since we last quit polling packets from the socket queue)
    public UdpClockStamp ProcessingInducedLag { get; private set; }

    protected ErrorCondition ErrorCondition;

    protected readonly PooledList<TConnection> ConnectionList;
    protected readonly PooledList<UdpConnection> DisconnectPendingList;

    protected readonly ConcurrentDictionary<int, TConnection> AddressHashTable;
    protected readonly ConcurrentDictionary<int, TConnection> ConnectCodeHashTable;

    private readonly Internal.PriorityQueue<UdpConnection, UdpClockStamp>? PriorityQueue;

    private UdpClockStamp MinimumScheduledStamp;

    private int RandomSeed;

    // TODO Pooling
    // private PooledQueue<PooledLogicalPacket> PoolCreatedList = new();
    // private PooledQueue<PooledLogicalPacket> PoolAvailableList = new();

    private int EventListBytes;
    private LinkedList<CallbackEvent> EventList = new();
    private LinkedList<CallbackEvent> AvailableEventList = new();

    private byte[] _buffer;
    private SocketAddress _socketAddress;

    public bool EventQueuing
    {
        // don't allow changing of queuing mode while inside GiveTime from another thread

        get
        {
            lock (_giveTimeGuard)
            {
                return Params.EventQueuing;
            }
        }
        set
        {
            lock (_giveTimeGuard)
            {
                Params.EventQueuing = value;
            }
        }
    }

    UdpManagerStatistics ManagerStats;
    UdpClockStamp ManagerStatsResetTime;

    public UdpManager(UdpParams udpParams, IServiceProvider serviceProvider)
    {
        // negative ClockSyncDelay is not allowed (makes no sense)
        ArgumentOutOfRangeException.ThrowIfNegative(udpParams.ClockSyncDelay);

        // crc bytes must be between 0 and 4
        ArgumentOutOfRangeException.ThrowIfNegative(udpParams.CrcBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(udpParams.CrcBytes, 4);

        // illegal encryption method specified
        for (var i = 0; i < Constants.EncryptPasses; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative((byte)udpParams.EncryptMethod[i]);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((byte)udpParams.EncryptMethod[i], 4);
        }

        // a hash table size greater than zero is required
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(udpParams.HashTableSize, 0);

        // raw packet size must be at least 64 bytes
        ArgumentOutOfRangeException.ThrowIfLessThan(udpParams.MaxRawPacketSize, 64);

        // incoming socket buffer size must be at least as big as one raw packet
        ArgumentOutOfRangeException.ThrowIfLessThan(udpParams.IncomingBufferSize, udpParams.MaxRawPacketSize);

        // keep alive delay can't be negative
        ArgumentOutOfRangeException.ThrowIfNegative(udpParams.KeepAliveDelay);

        // port alive delay can't be negative
        ArgumentOutOfRangeException.ThrowIfNegative(udpParams.PortAliveDelay);

        // must have at least 1 connection allowed
        ArgumentOutOfRangeException.ThrowIfLessThan(udpParams.MaxConnections, 1);

        // outgoing socket buffer must larger than a raw packet size
        ArgumentOutOfRangeException.ThrowIfLessThan(udpParams.OutgoingBufferSize, udpParams.MaxRawPacketSize);

        // packet history must be at least 1
        ArgumentOutOfRangeException.ThrowIfLessThan(udpParams.PacketHistoryMax, 1);

        // port cannot be negative
        ArgumentOutOfRangeException.ThrowIfNegative(udpParams.Port);

        // if encryption expansion is larger than raw packet size, we are screwed
        ArgumentOutOfRangeException.ThrowIfGreaterThan(udpParams.UserSuppliedEncryptExpansionBytes + udpParams.UserSuppliedEncryptExpansionBytes2, udpParams.MaxRawPacketSize);

        for (var i = 0; i < Constants.ReliableChannelCount; i++)
            ArgumentOutOfRangeException.ThrowIfLessThan(udpParams.Reliable[i].MaxOutstandingBytes, udpParams.MaxRawPacketSize);

        if (udpParams.Port == 0 && udpParams.PortRange != 0)
            throw new ArgumentOutOfRangeException("port range requires a valid port");

        Params = udpParams;

        _serviceProvider = serviceProvider;

        Params.MaxRawPacketSize = Math.Min(Params.MaxRawPacketSize, Constants.HardMaxRawPacketSize);

        if (Params.MaxDataHoldSize == -1)
            Params.MaxDataHoldSize = Params.MaxRawPacketSize;

        if (Params.PooledPacketSize == -1)
            Params.PooledPacketSize = Params.MaxRawPacketSize;

        Params.MaxDataHoldSize = Math.Min(Params.MaxDataHoldSize, Params.MaxRawPacketSize);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _driver = udpParams.UdpDriver ?? new UdpDriverWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _driver = udpParams.UdpDriver ?? new UdpDriverLinux();
        }

        ArgumentNullException.ThrowIfNull(_driver);

        Clock();

        RandomSeed = (int)CachedClock;

        LastReceiveTime = 0;
        LastSendTime = 0;
        LastEmptySocketBufferStamp = 0;
        ProcessingInducedLag = 0;
        MinimumScheduledStamp = 0;
        EventListBytes = 0;

        if (!udpParams.AvoidPriorityQueue)
            PriorityQueue = new Internal.PriorityQueue<UdpConnection, UdpClockStamp>(Params.MaxConnections);

        ConnectionList = new PooledList<TConnection>();
        DisconnectPendingList = new PooledList<UdpConnection>();

        AddressHashTable = new ConcurrentDictionary<int, TConnection>(Environment.ProcessorCount, Params.HashTableSize);

        // rarely used, so make it a fraction of the main tables size
        ConnectCodeHashTable = new ConcurrentDictionary<int, TConnection>(Environment.ProcessorCount, Math.Max(Params.HashTableSize / 5, 10));

        if (Params.PortRange == 0)
        {
            CreateAndBindSocket(Params.Port);
        }
        else
        {
            var r = Random() % Params.PortRange;

            for (var i = 0; i < Params.PortRange; i++)
            {
                CreateAndBindSocket(Params.Port + ((r + i) % Params.PortRange));

                if (ErrorCondition != ErrorCondition.CouldNotBindSocket)
                    break;
            }
        }

        _socketAddress = new SocketAddress(AddressFamily.InterNetwork);
        _buffer = GC.AllocateArray<byte>(Params.MaxRawPacketSize, true);
    }

    protected void CloseSocket()
    {
        _driver.SocketClose();
    }

    protected void CreateAndBindSocket(int usePort)
    {
        CloseSocket();

        ErrorCondition = ErrorCondition.None;

        if (!_driver.SocketOpen(usePort, Params.IncomingBufferSize, Params.OutgoingBufferSize, Params.BindIpAddress))
            ErrorCondition = ErrorCondition.CouldNotBindSocket;
    }

    private UdpClockStamp Clock()
    {
        lock (_clockGuard)
        {
            return CachedClock = _driver.Clock();
        }
    }

    private UdpClockStamp ClockElapsed(UdpClockStamp stamp)
    {
        return UdpMisc.ClockDiff(stamp, Clock());
    }

    public uint LocalSyncStampLong()
    {
        return (uint)Clock();
    }

    public ushort LocalSyncStampShort()
    {
        return (ushort)Clock();
    }

    public UdpClockStamp CachedClockElapsed(UdpClockStamp start)
    {
        return UdpMisc.ClockDiff(start, CachedClock);
    }

    public bool GiveTime(int maxPollingTime = 500, bool giveConnectionsTime = true)
    {
        lock (_giveTimeGuard)
        {
            Clock();

            if (Params.EventQueuing && EventList.Count > 0)
            {
                // if we have events queued and we are not in queuing mode, we must deliver those events
                // before we can actually do a give time (this should only happen when the queuing mode is changed on the fly)
                DeliverEvents(maxPollingTime);
                return true;
            }

            lock (_statsGuard)
            {
                ManagerStats.Iterations++;
            }

            var found = false;

            if (maxPollingTime != 0)
            {
                var start = CachedClock;

                while (true)
                {
                    var curStamp = CachedClock;

                    // TODO: Packet History

                    // TODO: Simulation

                    var res = _driver.SocketReceive(_buffer, _socketAddress);

                    if (res < 0)
                    {
                        LastEmptySocketBufferStamp = CachedClock;
                        break;
                    }

                    lock (_statsGuard)
                    {
                        LastReceiveTime = curStamp;
                        ManagerStats.BytesReceived += res;
                        ManagerStats.PacketsReceived++;
                    }

                    var data = _buffer.AsSpan(0, res);

                    // if the application takes too long to process packets, or doesn't give the UdpManager frequent enough time via GiveTime
                    // then it's possible that we will have a clock-sync packet that is sitting in the socket buffer waiting to be processed
                    // we don't want the applications inability to give us frequent processing time to totally whack up the clock sync stuff
                    // so we have the clock-sync code ignore clock-sync packets that get stalled in the socket buffer for too long because our
                    // application is busy processing other packets that were queued before it, or because the application paused for a long
                    // time before calling GiveTime.
                    // note: this is intended to prevent cpu induced stalls from causing a sync packet to appear to take longer.  For example
                    // if the player is on a modem, it's possible for the socket-buffer to fill up while the application is stalled and cause the
                    // the sync-packet to actually get stalled at the terminal buffer on the other end up of the modem.  When the application starts
                    // processing again, it will empty the socket-buffer, but then the get an empty-socket-buffer briefly until the terminal server
                    // can send the rest of the buffered packets on over.  A large client side receive socket buffer may help in this regard.
                    ProcessingInducedLag = CachedClockElapsed(LastEmptySocketBufferStamp);

                    found = true;

                    ProcessRawPacket(_socketAddress, data);

                    if (ClockElapsed(start) >= maxPollingTime)
                    {
                        lock (_statsGuard)
                        {
                            ManagerStats.MaxPollingTimeExceeded++;
                        }

                        break;
                    }
                }
            }

            if (giveConnectionsTime)
            {
                if (PriorityQueue is not null)
                {
                    // give time to everybody in the priority-queue that needs it
                    var curPriority = CachedClock;

                    // at the time we start processing the priority queue, we should effectively be taking a snap-shot
                    // of everybody who needs time, before we give anybody time.  Otherwise, it is possible that in the
                    // process of giving one connection time, another connection could get bumped up the queue to the point
                    // where it needs time now as well (for example, one connection sending another connection data during the
                    // give time phase).  Although very rare, in theory this could result in an infinite loop situation.
                    // To solve this, we simply set the earliest time period that somebody can schedule for to 1 ms after
                    // the current time stamp that we are processing, effectively making it impossible for any connection
                    // to be given time twice in the same interation of the loop below

                    lock (_connectionGuard)
                    {
                        MinimumScheduledStamp = curPriority + 1;
                    }

                    var processed = 0;

                    while (true)
                    {
                        UdpConnection? top;

                        lock (_connectionGuard)
                        {
                            top = PriorityQueue.TopRemove(curPriority);
                        }

                        if (top is null)
                            break;

                        top.GiveTime();

                        processed++;
                    }

                    lock (_statsGuard)
                    {
                        ManagerStats.PriorityQueueProcessed += processed;
                        ManagerStats.PriorityQueuePossible += ConnectionList.Count;
                    }
                }
                else
                {
                    foreach (var con in ConnectionList)
                    {
                        con.GiveTime();
                    }
                }

                ProcessDisconnectPending();
            }

            // TODO Simulation

            return found;
        }
    }

    private void DeliverEvents(int maxProcessingTime)
    {
        var start = Clock();

        while (true)
        {
            var ce = EventListPop();

            if (ce is null)
                break;

            switch (ce.EventType)
            {
                case CallbackEventType.RoutePacket:
                    {
                        if (ce.Payload is null)
                            break;

                        ce.Source?.OnRoutePacket(ce.Payload.GetDataPtr());
                    }
                    break;

                case CallbackEventType.ConnectComplete:
                    {
                        ce.Source?.OnConnectComplete();
                    }
                    break;

                case CallbackEventType.Terminated:
                    {
                        ce.Source?.OnTerminated();
                    }
                    break;

                case CallbackEventType.CrcReject:
                    {
                        if (ce.Payload is null)
                            break;

                        var data = ce.Payload.GetDataPtr();

                        ce.Source?.OnCrcReject(data.Slice(0, ce.Payload.GetDataLen()));
                    }
                    break;

                case CallbackEventType.PacketCorrupt:
                    {
                        if (ce.Payload is null)
                            break;

                        var data = ce.Payload.GetDataPtr();

                        ce.Source?.OnPacketCorrupt(data.Slice(0, ce.Payload.GetDataLen()), ce.Reason);
                    }
                    break;

                case CallbackEventType.ConnectRequest:
                    {
                        if (ce.Source is null)
                            break;

                        if (!OnConnectRequest(ce.Source))
                            ce.Source.InternalDisconnect(0, DisconnectReason.ConnectionRefused);

                    }
                    break;

                default:
                    break;
            }

            if (ClockElapsed(start) >= maxProcessingTime)
            {
                lock (_statsGuard)
                {
                    ManagerStats.MaxDeliveryTimeExceeded++;
                }

                break;
            }
        }
    }

    private void ProcessDisconnectPending()
    {
        lock (_disconnectPendingGuard)
        {
            DisconnectPendingList.RemoveAll(x => x.Status == Status.Disconnected);
        }
    }

    protected void ProcessRawPacket(SocketAddress socketAddress, Span<byte> data)
    {
        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte zeroByte))
            return;

        if (!reader.TryRead(out byte packetType))
            return;

        if (data.Length == 2 && zeroByte == 0 && packetType == (byte)UdpPacketType.PortAlive)
            return;

        if (data.Length == 2 && zeroByte == 0 && packetType == (byte)UdpPacketType.ServerStatus)
        {
            OnServerStatusRequest(socketAddress);
            return;
        }

        var con = AddressGetConnection(socketAddress);

        if (con == null)
        {
            if (data.Length == 0)
                return;

            if (data.Length >= 6 && zeroByte == 0 && packetType == (byte)UdpPacketType.Unknown)
                return;

            if (zeroByte == 0 && packetType == (byte)UdpPacketType.Connect)
            {
                if (ConnectionList.Count >= Params.MaxConnections)
                    return;

                // Skip protocol version
                reader.Advance(4);

                if (!reader.TryReadInt32(out int connectCode))
                    return;

                var newCon = ActivatorUtilities.CreateInstance<TConnection>(_serviceProvider, this, socketAddress, connectCode);

                if (newCon is not null)
                {
                    AddConnection(newCon);

                    newCon.GiveTime();
                    newCon.ProcessRawPacket(data);

                    CallbackConnectRequest(newCon);
                }
            }
            else
            {
                if (Params.AllowPortRemapping)
                {
                    if (zeroByte == 0 && packetType == (byte)UdpPacketType.RequestRemap)
                    {
                        // ok, we got a packet from somebody, that we don't know who they are, but, it appears they are asking
                        // for their address/port to be remapped.  If we allow port (and/or address) remapping, then go ahead
                        // an honor their request if possible

                        if (!reader.TryReadInt32(out int connectCode))
                            return;

                        if (!reader.TryReadInt32(out int encryptCode))
                            return;

                        var curCon = ConnectCodeGetConnection(connectCode);

                        if (curCon is not null)
                        {
                            var tempEndPoint = new IPEndPoint(IPAddress.Any, 0);
                            var ipEndPoint = (IPEndPoint)tempEndPoint.Create(socketAddress);

                            if (Params.AllowAddressRemapping || curCon.EndPoint.Address == ipEndPoint.Address)
                            {
                                // one final security check to ensure these are really the same connection, compare encryption codes

                                if (curCon.ConnectionConfig.EncryptCode == encryptCode)
                                {
                                    // remapping is allowed, remap ourselves to the address of the incoming request

                                    lock (_connectionGuard)
                                    {
                                        AddressHashTable.Remove(AddressHashValue(curCon.SocketAddress), out _);

                                        curCon.EndPoint = ipEndPoint;
                                        curCon.SocketAddress = socketAddress;

                                        AddressHashTable.TryAdd(AddressHashValue(curCon.SocketAddress), curCon);
                                    }

                                    return;
                                }
                            }
                        }
                    }
                }

                // got a packet from somebody and we don't know who they are and the packet we got was not a connection request
                // just in case they are a previous client who thinks they are still connected, we will send them an internal
                // packet telling them that we don't know who they are
                if (Params.ReplyUnreachableConnection)
                {
                    // do not reply back with unreachable if the packet coming in is a terminate or unreachable packet itself
                    if (zeroByte != 0 || (zeroByte == 0 && packetType != (byte)UdpPacketType.UnreachableConnection && packetType != (byte)UdpPacketType.Terminate))
                    {
                        // since we do not have a connection-object associated with this incoming packet, there is no way we could
                        // encrypt it or add CRC bytes to it, since we have no idea what the other end of the connection is expecting
                        // in this regard.  As such, the UnreachableConnection packet (like the connect and confirm packets) is one
                        // of those internal packet types that is designated as not being encrypted or CRC'ed.
                        Span<byte> buf = [0, (byte)UdpPacketType.UnreachableConnection];
                        ActualSend(buf, buf.Length, socketAddress);
                    }
                }
            }

            return;
        }

        con.ProcessRawPacket(data);
    }

    private TConnection? ConnectCodeGetConnection(int connectCode)
    {
        lock (_connectionGuard)
        {
            if (ConnectCodeHashTable.TryGetValue(connectCode, out var con))
                return con;

            return null;
        }
    }

    private void CallbackConnectRequest(TConnection con)
    {
        if (Params.EventQueuing)
        {
            var ce = AvailableEventBorrow();
            ce.SetEventData(CallbackEventType.ConnectRequest, con);
            EventListAppend(ce);
        }
        else
        {
            if (!OnConnectRequest(con))
                con.InternalDisconnect(0, DisconnectReason.ConnectionRefused);
        }
    }

    protected TConnection? AddressGetConnection(SocketAddress socketAddress)
    {
        lock (_connectionGuard)
        {
            if (AddressHashTable.TryGetValue(AddressHashValue(socketAddress), out var udpConnection))
                return udpConnection;
        }

        return null;
    }

    protected int AddressHashValue(SocketAddress socketAddress)
    {
        return socketAddress.GetHashCode();
    }

    public void AddConnection(TConnection con)
    {
        lock (_connectionGuard)
        {
            ConnectionList.Add(con);
            AddressHashTable.TryAdd(AddressHashValue(con.SocketAddress), con);
            ConnectCodeHashTable.TryAdd(con.ConnectCode, con);
        }
    }

    public int Random()
    {
        return UdpMisc.Random(ref RandomSeed);
    }

    public void ActualSend(ReadOnlySpan<byte> data, int dataLen, SocketAddress socketAddress)
    {
        LastSendTime = CachedClock;

        ManagerStats.BytesSent += dataLen;
        ManagerStats.PacketsSent++;

        // TODO: Simulation

        ActualSendHelper(data, dataLen, socketAddress);
    }

    private void ActualSendHelper(ReadOnlySpan<byte> data, int dataLen, SocketAddress socketAddress)
    {
        // TODO: Simulation

        if (!_driver.SocketSend(data.Slice(0, dataLen), socketAddress))
        {
            // assume a send error is a socket overflow, which we track only for statistical purposes
            lock (_statsGuard)
            {
                ManagerStats.SocketOverflowErrors++;
            }
        }
    }

    public void SetPriority(UdpConnection con, UdpClockStamp stamp)
    {
        lock (_connectionGuard)
        {
            // do not ever let anybody schedule themselves for processing time sooner then mMinimumScheduledStamp
            // otherwise, they could end up getting processing time multiple times in a single UdpManager::GiveTime iteration
            // of the priority queue, which under odd circumstances could result in an infinite loop
            if (stamp < MinimumScheduledStamp)
                stamp = MinimumScheduledStamp;

            PriorityQueue?.Add(con, stamp);
        }
    }

    public LogicalPacket CreatePacket(Span<byte> data, int dataLen, Span<byte> data2 = default, int dataLen2 = 0)
    {
        if (Params.PooledPacketMax > 0)
        {
            var totalLen = dataLen + dataLen2;

            if (totalLen <= Params.PooledPacketSize)
            {
                // TODO Pooling
                /* if (!PoolAvailableList.TryDequeue(out var lp))
                {
                    // create a new pooled packet to fulfil request
                    lp = new PooledLogicalPacket(Params.PooledPacketSize);
                } */

                var lp = new PooledLogicalPacket(Params.PooledPacketSize);

                lp.SetData(data, dataLen, data2, dataLen2);

                return lp;
            }
        }

        return UdpMisc.CreateQuickLogicalPacket(data, dataLen, data2, dataLen2);
    }

    public void CallbackRoutePacket(UdpConnection con, Span<byte> data)
    {
        if (Params.EventQueuing)
        {
            var ce = AvailableEventBorrow();
            var packet = CreatePacket(data, data.Length);
            ce.SetEventData(CallbackEventType.RoutePacket, con, packet);
            EventListAppend(ce);
        }
        else
        {
            con.OnRoutePacket(data);
        }
    }

    public void CallbackCrcReject(UdpConnection con, Span<byte> data)
    {
        if (Params.EventQueuing)
        {
            var ce = AvailableEventBorrow();
            var packet = CreatePacket(data, data.Length);
            ce.SetEventData(CallbackEventType.CrcReject, con, packet);
            EventListAppend(ce);
        }
        else
        {
            con.OnCrcReject(data);
        }
    }

    public void CallbackPacketCorrupt(UdpConnection con, Span<byte> data, UdpCorruptionReason reason)
    {
        if (Params.EventQueuing)
        {
            var ce = AvailableEventBorrow();
            var packet = CreatePacket(data, data.Length);
            ce.SetEventData(CallbackEventType.PacketCorrupt, con, packet);
            ce.Reason = reason;
            EventListAppend(ce);
        }
        else
        {
            con.OnPacketCorrupt(data, reason);
        }
    }

    public void CallbackConnectComplete(UdpConnection con)
    {
        if (Params.EventQueuing)
        {
            var ce = AvailableEventBorrow();
            ce.SetEventData(CallbackEventType.ConnectComplete, con);
            EventListAppend(ce);
        }
        else
        {
            con.OnConnectComplete();
        }
    }

    public void SendPortAlive(SocketAddress socketAddress)
    {
        Span<byte> buf = [0, (byte)UdpPacketType.PortAlive];
        _driver.SocketSendPortAlive(buf, socketAddress);
    }

    public void KeepUntilDisconnected(UdpConnection udpConnection)
    {
        lock (_disconnectPendingGuard)
        {
            DisconnectPendingList.Add(udpConnection);
        }
    }

    public void RemoveConnection(UdpConnection con)
    {
        lock (_connectionGuard)
        {
            if (PriorityQueue is not null)
                PriorityQueue.Remove(con);

            ConnectCodeHashTable.Remove(con.ConnectCode, out _);

            if (!AddressHashTable.TryRemove(AddressHashValue(con.SocketAddress), out var conInstance))
                return;

            ConnectionList.Remove(conInstance);
        }
    }

    public void CallbackTerminated(UdpConnection con)
    {
        if (Params.EventQueuing)
        {
            var ce = AvailableEventBorrow();
            ce.SetEventData(CallbackEventType.Terminated, con);
            EventListAppend(ce);
        }
        else
        {
            con.OnTerminated();
        }
    }

    public virtual bool OnConnectRequest(UdpConnection udpConnection)
    {
        return false;
    }

    private CallbackEvent AvailableEventBorrow()
    {
        lock (_availableEventGuard)
        {
            var ce = AvailableEventList.First?.Value;

            if (ce is not null)
                AvailableEventList.RemoveFirst();
            else
                ce = new CallbackEvent();

            return ce;
        }
    }

    private void AvailableEventReturn(CallbackEvent ce)
    {
        lock (_availableEventGuard)
        {
            if (AvailableEventList.Count < Params.CallbackEventPoolMax)
            {
                AvailableEventList.AddFirst(ce);
            }
        }
    }

    private void EventListAppend(CallbackEvent ce)
    {
        lock (_eventListGuard)
        {
            EventList.AddLast(ce);

            if (ce.Payload is not null)
            {
                EventListBytes += ce.Payload.GetDataLen();
            }
        }
    }

    private CallbackEvent? EventListPop()
    {
        lock (_eventListGuard)
        {
            var ce = EventList.First?.Value;

            if (ce is not null && ce.Payload is not null)
            {
                EventList.RemoveFirst();
                EventListBytes -= ce.Payload.GetDataLen();
            }

            return ce;
        }
    }

    public TConnection? EstablishConnection(string serverAddress, int serverPort = 0, UdpClockStamp timeout = 0)
    {
        lock (_giveTimeGuard)
        {
            var portIndex = serverAddress.IndexOf(':');

            if (portIndex > 0)
            {
                int.TryParse(serverAddress.AsSpan(portIndex + 1), out serverPort);
                serverAddress = serverAddress.Substring(0, portIndex);
            }

            // can't connect to no ip/port
            if (string.IsNullOrEmpty(serverAddress) || serverPort == 0)
                return null;

            if (ConnectionList.Count >= Params.MaxConnections)
                return null;

            if (!_driver.GetHostByName(out var destIp, serverAddress))
                return null; // could not resolve name

            var endPoint = new IPEndPoint(destIp, serverPort);
            var socketAddress = endPoint.Serialize();

            // first, see if we already have a connection object managing this ip/port, if we do, then fail
            var con = AddressGetConnection(socketAddress);

            if (con is not null)
                return null;

            con = ActivatorUtilities.CreateInstance<TConnection>(_serviceProvider, this, socketAddress, timeout);

            AddConnection(con);

            return con;
        }
    }

    public void GetStats(out UdpManagerStatistics stats)
    {
        lock (_statsGuard)
        {
            stats = ManagerStats;

            // TODO Pooling
            // stats.PoolAvailable = PoolAvailableList.Count;
            // stats.PoolCreated = PoolCreatedList.Count;

            lock (_disconnectPendingGuard)
            {
                stats.DisconnectPendingCount = DisconnectPendingList.Count;
            }

            stats.ConnectionCount = ConnectionList.Count;
            stats.EventListCount = EventList.Count;
            stats.EventListBytes = EventListBytes;
            stats.ElapsedTime = CachedClockElapsed(ManagerStatsResetTime);
        }
    }

    public void ResetStats()
    {
        lock (_statsGuard)
        {
            ManagerStatsResetTime = CachedClock;
            ManagerStats.Reset();
        }
    }

    public void IncrementCrcRejectedPackets()
    {
        lock (_statsGuard)
        {
            ManagerStats.CrcRejectedPackets++;
        }
    }
    public void IncrementOrderRejectedPackets()
    {
        lock (_statsGuard)
        {
            ManagerStats.OrderRejectedPackets++;
        }
    }

    public void IncrementDuplicatePacketsReceived()
    {
        lock (_statsGuard)
        {
            ManagerStats.DuplicatePacketsReceived++;
        }
    }

    public void IncrementResentPacketsAccelerated()
    {
        lock (_statsGuard)
        {
            ManagerStats.ResentPacketsAccelerated++;
        }
    }

    public void IncrementResentPacketsTimedOut()
    {
        lock (_statsGuard)
        {
            ManagerStats.ResentPacketsTimedOut++;
        }
    }

    public void IncrementApplicationPacketsSent()
    {
        lock (_statsGuard)
        {
            ManagerStats.ApplicationPacketsSent++;
        }
    }

    public void IncrementApplicationPacketsReceived()
    {
        lock (_statsGuard)
        {
            ManagerStats.ApplicationPacketsReceived++;
        }
    }

    public void IncrementCorruptPacketErrors()
    {
        lock (_statsGuard)
        {
            ManagerStats.CorruptPacketErrors++;
        }
    }

    public virtual void OnServerStatusRequest(SocketAddress socketAddress)
    {
    }

    public virtual void Dispose()
    {
        lock (_connectionGuard)
        {
            var connectionList = ConnectionList.ToFrozenSet();

            foreach (var connection in connectionList)
            {
                if (connection is null)
                    continue;

                connection.InternalDisconnect(0, DisconnectReason.ManagerDeleted);
            }
        }

        lock (_disconnectPendingGuard)
        {
            DisconnectPendingList.Clear();
        }

        // TODO Pooling
        /* lock (_poolGuard)
        {
            PoolCreatedList.Clear();
        } */

        if (Params.LingerDelay != 0)
        {
            // sleep momentarily before closing socket to give it a chance to empty the socket buffer if there is something in it
            _driver.Sleep(Params.LingerDelay);
        }

        CloseSocket();
    }
}