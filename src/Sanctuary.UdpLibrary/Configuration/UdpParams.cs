using Sanctuary.UdpLibrary.Internal;
using Sanctuary.UdpLibrary.Abstractions;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.UdpLibrary.Configuration;

public class UdpParams
{
    /// <summary>
    /// This is the maximum number of connections that can be established by this manager, any incoming/outgoing connections
    /// over this limit will be refused.  On the client side, this typically only needs to be set to 1, though there
    /// is little harm in setting this number larger.
    /// </summary>
    /// <remarks>default = 10</remarks>
    public int MaxConnections;

    /// <summary>
    /// This is the port number that this manager will use for all incoming and outgoing data. On the client side
    /// this is typically set to 0, which causes the manager object to randomly pick an available port.  On the server
    /// side, this port should be set to a specific value as it will represent the port number that clients will use
    /// to connect to the server (ie. the listening port).  It's generally a good idea to give the user on the client
    /// side the option of fixing this port number at a specific value as well as it is often necessary for them to
    /// do so in order to navigate company firewalls which may have specific port numbers open to them for this purpose.
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int Port;

    /// <summary>
    /// If port is not zero, setting this to a value greater than zero causes it to randomly pick a port in the range
    /// (port to port+portRange).  This is desirable when you wish to have the manager bind to a random port within
    /// a specified range.  If you specify a portRange to use, then port must not be 0 (since 0 means to let the OS
    /// pick the port to use).  0 = don't use a range, bind only to the port specified in <see cref="Port"/> setting.
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int PortRange;

    /// <summary>
    /// The size of the outgoing socket buffer.  The client will want to set this fairly small (32k or so), but the server
    /// will want to set this fairly large (512k)
    /// </summary>
    /// <remarks>default = 64k</remarks>
    public int OutgoingBufferSize;

    /// <summary>
    /// The size of the incoming socket buffer.  The client will want to set this fairly small (32k or so), but the server
    /// will want to set this fairly large (512k)
    /// </summary>
    /// <remarks>default = 64k</remarks>
    public int IncomingBufferSize;

    /// <summary>
    /// The purpose of the packet history is to make debugging easier.  Sometimes a processed packet will cause the server
    /// to crash (due to a bug or possibly just corruption).  Typically the application will put an exception handler around
    /// the main loop and call <see cref="UdpManager{TConnection}.DumpPacketHistory"/> when it is triggered.  This will dump a history of the last
    /// packets received such that they can be analysed by hand to determine if one of them caused the problem and why.
    /// The packet history is done at the raw packet level (before logical packet re-assembly).  This is the number of raw
    /// packets to buffer in the history.  Typically this can be set fairly small (maybe 1000) since packets older than
    /// that have little debug value. (uses maxRawPacketSize * thisValue of memory)
    /// </summary>
    /// <remarks>default = 4</remarks>
    public int PacketHistoryMax;

    /// <summary>
    /// How long a connection will wait before sending a keep-alive packet to the other side of the connection
    /// set this to 0 to never send keep-alive packets (typically the server will set this to 0 and never keep alive
    /// the client, but the client will typically set this some value that will ensure that the server will not kick them
    /// for inactivity.  Keep alive packets are only sent if no other data is being sent.
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int KeepAliveDelay;

    /// <summary>
    /// This is very similar to the keep alive delay, but serves a completely different purpose and may not have the desired
    /// effect on all platforms.  The purpose of the <see cref="KeepAliveDelay"/> above is generally to keep data flowing to the server
    /// so it knows that the client is still there.  In this manner if the server doesn't get data within a certain period
    /// of time, it can know that the connection is probably dead.  Sometimes it is the case that the server does not need
    /// to be kept alive, or at least kept alive very often (like for a chat server perhaps where nobody is talking much).
    /// For some people, it may be necessary to send data more frequently in order to keep their NAT mapping fresh, or their
    /// firewall software happy.  However, we don't want to be in a situation where our server is receiving a lot more data
    /// than it needs to just so these people can keep their port open.  I have seen NAT's that lose mappings in as short
    /// as 20 seconds.  What this feature does is a bit tricky.  It changes the time-to-live (TTL) for a special keep-alive
    /// packet to some small value (4) which is enough for the packet to get past firewalls and NAT's, but not make it all
    /// the way to our server.  In this manner, the port gets kept alive, but we don't waste bandwidth with these packets.
    /// These special packets are not counted statistically in any way and they do not reset any timers of any kind.  Their 
    /// sole purpose is to keep the port alive on the client side.  Any other data (include standard keepAlive packets will
    /// reset the timer for this packet, so obviously the <see cref="PortAliveDelay"/> must be smaller than the <see cref="KeepAliveDelay"/> in order
    /// to be meaningful.
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int PortAliveDelay;

    /// <summary>
    /// Whether this UdpManager should send back an unreachable-connection packet when it receives incoming data from
    /// a destination it does not have a <see cref="UdpConnection"/> object representing.  This is the equivalent of a port-unreachable
    /// ICMP error packet, only taken up one level further to the virtual-connection object (<see cref="UdpConnection"/>).  Imagine a
    /// server that has terminated a client's connection, but the client didn't get notice that such termination had occurred.
    /// The client would still think the connection was good and continue to try and send data to the server, but the data
    /// would simply be getting lost and queued up indefinitely on the client.  The client doesn't get port unreachable errors
    /// since the server is using the same port for lots of people.  Having this true causes the server to notify the client
    /// that its connection is dead.  If a client finds out it is terminated in this manner, it will set its disconnect reason
    /// to <see cref="DisconnectReason.UnreachableConnection"/>.
    /// </summary>
    /// <remarks>default = true</remarks>
    public bool ReplyUnreachableConnection;

    /// <summary>
    /// The UdpLibrary supports a feature whereby a connection can remap its ip/port on-the-fly.  How it works is, if a client
    /// gets back a connection-unreachable error, it will send out a request for the server to remap its connection to the
    /// new port that it is coming from.  This will happen if a NAT expires and the next outgoing packet causes a different
    /// port-mapping to be selected.  The UdpLibrary can recover from this situation.  The <see cref="IcmpErrorRetryPeriod"/> must be
    /// set to a reasonably high value, such that the client has time to send data and request the remapping before icmp
    /// errors are processed.  This value determines not only whether a server will honour a remapping request, but also
    /// whether the client will attempt a remapping request.  The terms client as server are used loosely here, as it is
    /// possible for the server to request the remapping (though it is extremely unlikely that the server end
    /// of a connection will have its port changed on-the-fly).
    /// </summary>
    /// <remarks>default = true</remarks>
    public bool AllowPortRemapping;

    /// <summary>
    /// This is the same as <see cref="AllowPortRemapping"/>, only it allows the full IP address to be remapped on-the-fly.  I
    /// recommend that you do NOT enable this, as it represents a fairly serious security loophole, whereby a hacker could
    /// cause random people to be disconnected, and in theory possibly even hi-jack their connection.  The odds of this
    /// actually being able to happen are incredibly rare unless the hacker has been snooping your packet stream, as they
    /// would effectively have to guess two 32-bit random numbers (one generated by the client and one generated by the server)
    /// </summary>
    /// <remarks>default = false</remarks>
    public bool AllowAddressRemapping;

    /// <summary>
    /// How long (in milliseconds) the manager will ignore ICMP errors to a particular connection before it will act upon
    /// them and terminate the connection.  When a packet is successfully received into the connection in question, the
    /// retry period is reset, such that the next time an ICMP error comes in, it has another period of time for it
    /// to resolve the issue.  This servers a couple purpose, 1) it can allow for momentary outages that can be recovered
    /// from in short order, and 2) it is necessary for the port-remapping feature to work properly in situations where
    /// the server may send data to the old-port before the remapping negotiations are completed.  In order for the
    /// remapping feature to work properly, this value should be set to larger than the longest time a connection typically
    /// goes without receiving data from the other side. (0 = no grace period)
    /// </summary>
    /// <remarks>default = 5000</remarks>
    public int IcmpErrorRetryPeriod;

    /// <summary>
    /// How long a connection will go without receiving any data before it will terminate the connection
    /// set this to 0 to never have the connection terminated automatically for receiving no data.
    /// the application will receive an OnTerminated callback when the connection is terminated due to this timeout
    /// this setting can be overridden on a per-connection basis by changing <see cref="UdpConnection.NoDataTimeout"/>
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int NoDataTimeout;

    /// <summary>
    /// When reliable data is sent, it remains in the queue until it is acknowledged by the other side.  When you query
    /// the reliable channel status, you can find out the age of the oldest piece of data in the queue that has been sent
    /// yet has not been acknowledged yet.  As a general rule, if things are operating correctly, it should be very rare
    /// for something that has been sent to not be acknowledged within a few seconds, even if resending had to occur.
    /// Eventually, the sender could use this statistic to determine that the other side is no longer talking and terminate
    /// the connection.  In past version of the UdpLibrary, the sending-application has checked this statistic itself.  This
    /// parameter will cause the UdpLibrary to monitor this for you and automatically change the connection to a disconnected
    /// state when the value goes over this setting (in milliseconds) (0 means do not perform this check at all)
    /// The default is set fairly liberally, on client-side connections, you could safely set this to as low as 30 seconds
    /// allowing for quicker realization of lost connections.  Often times the connection will realize it is dead much quicker
    /// for other reasons.  When disconnected due to this, the disconnect reason is set to <see cref="DisconnectReason.UnacknowledgedTimeout"/>
    /// </summary>
    /// <remarks>default = 90000</remarks>
    public int OldestUnacknowledgedTimeout;

    /// <summary>
    /// Maximum number of bytes that are allowed to be pending in all the reliable channels combined
    /// before it will terminate the connection with a <see cref="DisconnectReason.ReliableOverflow"/>.  If this value is set to
    /// zero, then it will never overflow (it will run out of memory and crash first).  If your application wants
    /// to do something other than disconnect on this condition, then the application will have to periodically check the status
    /// itself using <see cref="UdpConnection.TotalPendingBytes"/> and act as appropriate.
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int ReliableOverflowBytes;

    /// <summary>
    /// How long a connection will hold onto outgoing data in hopes of bundling together future outgoing data in the same
    /// raw packet (specified in milliseconds)
    /// setting this to 0 will cause it to effectively flush at the end of every frame.  This is generally desirable in
    /// cases where frame-rates are slow (less than 10 fps), or for internal LAN connections.
    /// </summary>
    /// <remarks>default = 50</remarks>
    public int MaxDataHoldTime;

    /// <summary>
    /// How much data a connection will hold onto before sending out a raw packet
    /// (0 = no multi-packet buffering, all application sends result in immediate raw packet sends)
    /// (-1 = use same value as <see cref="MaxRawPacketSize"/>)
    /// this value will be effectively ignored if it is larger than the <see cref="MaxRawPacketSize"/> specified below)
    /// </summary>
    /// <remarks>default = -1</remarks>
    public int MaxDataHoldSize;

    /// <summary>
    /// This is the maximum raw physical packet size that the UdpLibrary will attempt to send or can receive.
    /// In very old versions of the UdpLibrary, both sides needed to have this value configured identically.  In the
    /// newer versions, this value is negotiated during connection negotiation and the actual size used is the smaller
    /// value of what the client has configured and what the server has configured.  As a general rule, any connection
    /// that talks off of the local-area-network should leave this at the default value of 512.  Internal server to server
    /// connections should set this number to 1460 bytes, as that represents the maximum size that can fit in a normal
    /// ethernet frame.  If you are unsure, set it to 512, since this value is guaranteed to work on any IP based network.
    /// Larger values will make more efficient use of internal network resources if you know that all connections are
    /// going to be on our internal network.
    /// </summary>
    /// <remarks>default = 512, minimum = 64</remarks>
    public int MaxRawPacketSize;

    /// <summary>
    /// How large the hash-table is for looking up connections.  It takes 4*hashTableSize memory and it is recommended
    /// you set it fairly large to prevent collisions (10 times maximum number of connections should be fine)
    /// </summary>
    /// <remarks>default = 100</remarks>
    public int HashTableSize;

    /// <summary>
    /// Whether a priority queue should be used.  If a priority queue is not used, then every time
    /// <see cref="UdpManager{TConnection}.GiveTime"/> is called, every <see cref="UdpConnection"/> object gets processing time as well.
    /// It is thought that if traffic is heavy enough, that managing the priority queue may end up being more
    /// cpu time than giving everybody time (as it is possible everybody would end up getting time anyways)
    /// if not using a priority queue, it is recommended that you GiveTime only periodically (every 50ms for example)
    /// the more often you GiveTime, the more critical the priority-queue is at reducing load compared to not using it
    /// </summary>
    /// <remarks>default = false</remarks>
    public bool AvoidPriorityQueue;

    /// <summary>
    /// How often the client synchronizes its timing-clock to the servers (0 = never)(specified in ms).
    /// The server-side MUST specify this as 0, or else the server will attempt to synchronize it's clock with
    /// the client as well (which would just be a waste of packets generally, though would work).
    /// The client should generally always set this feature on by setting it to something like 45000 ms.
    /// The clock-sync is used to negotiate statistics as well, so if you are not using clock sync, then you
    /// will not be able to get packet loss/lag statistics for the connection.  If you are using it, then you will
    /// be able to get these statistics from either end of the connection.
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int ClockSyncDelay;

    /// <summary>
    /// When the UdpManager is destroyed and the socket is closed, all pending data in the socket is destroyed
    /// (because we are UDP and non-blocking, there is no lingering time).  The problem is, on the client side it
    /// is often the case that connection and manager are destroyed simultaneously.  This can often result in the
    /// final terminate-packet not making it to the server since the terminate packet will often get thrown away
    /// when the socket buffer is destroyed.  This delay will cause the UdpManager to sleep momentarily (for however
    /// long is specified in this option) in order to give the OS time to flush out that socket buffer before it
    /// destroys it.  I wish there were a way to select on whether a socket buffer was empty, but there isn't, and there
    /// is no way to query how full an outgoing socket buffer is.  As such, the only way to have a fair-chance of deliver
    /// of this final packet is to simply give up our time-slice momentarily and hope the OS pumps out that final message
    /// to the network before we wake up and destroy the socket.  By default we will have it sleep for 10ms, which does
    /// little more than give up our time-slice, but should be enough to get the job done in most cases.  Setting this
    /// value to 0 will cause it to not linger at all.  Again, however long you set this value to is how long the
    /// UdpManager destructor will block.
    /// </summary>
    /// <remarks>default = 10</remarks>
    public int LingerDelay;

    /// <summary>
    /// These two values control the number of packets that the UdpManager will create in its packet pool.  The packet pool
    /// is a means by which the UdpManager avoid allocating logical packets for every send, by instead using them from the pool.
    /// you need to specify the size of the packets that are in the pool.  Then, when somebody calls <see cref="UdpManager{TConnection}.CreatePacket"/>,
    /// if the packet being created is small enough and there is room available, it grabs one from the pool, otherwise it
    /// creates a new non-pooled one.  The largest the pool will ever grow is <see cref="PooledPacketMax"/>, and the memory used will be roughly
    /// <see cref="PooledPacketMax"/> * <see cref="PooledPacketSize"/>.  You should be generous with the pool packet size and the pool max in order
    /// to avoid having to do allocations as much as possible
    /// </summary>
    /// <remarks>default = 1000, (0 = don't use memory pooling at all)</remarks>
    public int PooledPacketMax;

    /// <summary>
    /// PooledPacketInitial is the number of packets to allocate in the initial pool.  The only reason to set this to 
    /// something other than the default, which is 0, is to avoid having your memory fragmented as the pool grows on demand.
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int PooledPacketInitial;

    /// <summary>
    /// As a general rule, ou should leave this at -1.  This is critical.  If the <see cref="PooledPacketSize"/> is smaller than the
    /// <see cref="MaxRawPacketSize"/>, then all the coalescing that occurs in the reliable channel will result in allocations.  You
    /// would just as well not have a pool if you don't set this at least as large as the <see cref="MaxRawPacketSize"/>.  If your application
    /// tends to send largish packets (larger than <see cref="MaxRawPacketSize"/>), setting this large enough to cover those might buy you
    /// some speed as well.
    /// </summary>
    /// <remarks>default = -1</remarks>
    public int PooledPacketSize;

    /// <summary>
    /// The maximum number of entries to allow in the callback event pool.  The callback event pool is only used
    /// if the application is queuing callback events for later delivery in the main thread.  This pool serves as
    /// an optimization by providing a pool of these objects to use.  Generally, this number should be set as large
    /// as the highest number of callback events that might come in between calls to have the events delivered.
    /// We do not pre-create these things, they are just created on demand.
    /// </summary>
    /// <remarks>default = 5000</remarks>
    public int CallbackEventPoolMax;

    /// <summary>
    /// Whether ICMP error packets should be used to determine if a connection has gone dead.
    /// When the destination machine is not available, or there is no process on the destination machine
    /// talking on the port, then a ICMP error packet will sometimes be returned to the client when a packet is sent.
    /// Processing ICMP errors will often allow the client machine to quickly determine that the other end of the
    /// connection is no longer reachable, allowing it to quickly change to a disconnected state.  The downside
    /// of having this feature enabled is that it is possible that if there is a network problem along the route, that
    /// the connection will be terminated, even though the hardware along the route may correct the problem by re-routing
    /// within a couple seconds.  If you are having problems with clients getting disconnected for ICMP errors (see
    /// disconnect reason), and you know the server should have remained reachable the entire time, then you might
    /// want to set this setting to false.  The only downside of setting this to false is that it might take the
    /// client a bit longer to realize when a server goes down.
    /// </summary>
    /// <remarks>default = true</remarks>
    public bool ProcessIcmpErrors;

    /// <summary>
    /// Whether ICMP errors should be used to terminate connection negotiations.  By default, this is set to 
    /// false, since generally when you are trying to establish a new connection (ie. negotiating), you are
    /// are willing to wait for timeout specified in the <see cref="UdpManager{TConnection}.EstablishConnection"/> call, since it may be a case
    /// that the client process gets started slightly sooner than the server process.
    /// </summary>
    /// <remarks>default = false</remarks>
    public bool ProcessIcmpErrorsDuringNegotiating;

    /// <summary>
    /// During connection negotiation, the client sends connect-attempt packets on a periodic basis until the
    /// server responds acknowledging the connection.  This value represents how often the client sends those packets.
    /// </summary>
    /// <remarks>default = 1000</remarks>
    public int ConnectAttemptDelay;

    /// <summary>
    /// If you call <see cref="UdpManager{TConnection}.ThreadStart"/> to start a background thread going calling GiveTime, it will sleep for
    /// this amount of time each iteration.  Generally there is no reason to change this value as going much faster
    /// doesn't buy you much, and going much slower doesn't save you much either.
    /// </summary>
    /// <remarks>default = 20</remarks>
    public int ThreadSleepTime;

    /// <summary>
    /// This settings allows you to bind the socket used by the library to a specific IP address on the machine.
    /// Normally, and by default, the library will bind the socket to any address in the machine.  This setting should
    /// not be messed with unless you really know what you are doing.  In multi-homed machines it is generally NOT
    /// necessary to bind to a particular address, even if there are firewall issues involved, and even if you want
    /// to limit traffic to a particular network (firewalls do a better job of serving that purpose).  If you are having
    /// problems communicating with a server on a multi-homed machine and think this might solve the problem, think again.
    /// You most likely need to configure the OS to route data appropriately, or make sure that internal network clients
    /// are connecting to the machine via the machines internal IP address (or vice versa).
    /// by default this string is empty meaning the socket is bound to any address.  To bind to specific IP address, it
    /// should be entered in a.b.c.d form (DNS names are not allowed).  Figuring out what IP addresses are in the machine
    /// and which one should be bound to is left as an exercise for the user.
    /// </summary>
    public string BindIpAddress;

    /// <summary>
    /// You need to specify the characteristics of the various reliable channels here, generally you will want
    /// to make sure the client and server sides set these characteristics the same, though it is technically not
    /// required.  Each channel decides locally whether it will accept out of order delivery on a particular channel or not.
    /// (note: out of order delivery is a tiny optimization that simply lets the channel deliver the packet the moment it
    /// arrives, even if previous packets have not yet arrived).  Likewise trickle-rates are for outgoing data only obviously.
    /// reliable channel managers are not actually created internally until data is actually sent on the channel, so there
    /// is no overhead associated with channels that are not used, and you need not specify characteristics for channels
    /// that you know you will not be using.
    /// </summary>
    /// <remarks>default = 400 packets in&out/200k outstanding, ordered, no trickle (all channels)</remarks>
    public UdpReliableConfig[] Reliable = new UdpReliableConfig[Constants.ReliableChannelCount];

    /// <summary>
    /// When user supplied encrypt and decrypt routines are specified, it becomes necessary to tell the UdpManager
    /// how many bytes the encryption process could possibly expand the chunk of data it was given.  Often times this
    /// will simply be 0, but if the user supplied routines attempt compression, then it's possible that expansion could
    /// could actually occur.  Typically I would have the compression routines put a leader-byte on the chunk of data
    /// specifying whether it was compressed or not.  Then if the compression didn't pan out, it could always abort and just
    /// prepend the byte in question and the rest of the data.  In that sort of algorithm, you would set this value to 1
    /// since the most it could ever expand beyond the original is the 1-byte indicator on the front.  It's possible that
    /// a particular encryption routine might want to append a 4-byte encryption key on the end of the chunk of data, in
    /// which case you would need to reserve 4 bytes.  This is necessary as it allows the udp library to guarantee that
    /// no packet will be larger than <see cref="MaxRawPacketSize"/>, and at the same time ensures that the destination buffers supplied
    /// to the encrypt/decrypt functions will have enough room.  Obviously this value is ignored if the encryptMethod
    /// is not set to <see cref="EncryptMethod.UserSupplied"/>.
    /// </summary>
    /// <remarks>default = 0</remarks>
    public int UserSuppliedEncryptExpansionBytes;
    public int UserSuppliedEncryptExpansionBytes2;

    /// <summary>
    /// Pointer to driver-object to use for this manager.  Typically, this driver object will create
    /// the <see cref="UdpPlatformDriver"/> object itself and chain the calls on through, plus do whatever else it wants;
    /// however, that is not required.  The application maintains ownership of this object and the object must
    /// not be destroyed by the application until the UdpManager using it is destroyed.
    /// default = null, meaning the UdpManager it will create it's own <see cref="UdpPlatformDriver"/> for use.
    /// </summary>
    public IUdpDriver? UdpDriver;

    /// <summary>
    /// Whether the UdpManager should be operating in event-queuing mode.  This setting can be changed on the fly
    /// by the application through the <see cref="UdpManager{TConnection}.EventQueuing"/> .  When in event queuing mode, all callbacks
    /// to the application via either the UdpConnectionHandler or the UdpManagerHandler are queued and only sent
    /// when the application makes a DeliverEvents call (or turns queuing mode off).  Queuing these events is not
    /// quite as efficient as processing them inline, so should generally only be done when the threading benefits
    /// are needed.  The main purpose of this feature is such that a background thread can GiveTime to the UdpManager
    /// on a regular basis, yet the applications main thread can call DeliverEvents only at times it is capable of
    /// processing the packets.  If your application's main loop consistently spins fast enough (say 10 fps), then I
    /// recommend not spinning a thread to give time to the UdpManager, and instead giving time in the main thread
    /// and operating in non-queuing mode.  See SetEventQueuing function call for more details.
    /// </summary>
    /// <remarks>default = false</remarks>
    public bool EventQueuing;

    /// <summary>
    /// Largest application packet to expect incoming on the wire.  This should generally be set liberally as it will
    /// disconnect any connection that tries to send a larger packet than this.  The purpose of this setting it to
    /// prevent hackers from fiddling with the packet-stream and make the server think it has an enormous (gigabytes)
    /// packet coming in, as it will allocate a buffer to hold the incoming packet, which could lead to a server crash.
    /// Since extremely large applications packets are rare, I have set the default limit to somewhat low, since most
    /// applications won't bother to mess with this setting.  If you legitimately send super-big packets, you may need
    /// to increase this number.
    /// </summary>
    /// <remarks>default = 10mb</remarks>
    public int IncomingLogicalPacketMax;

    public string ProtocolName;

    /// <summary>
    /// How many additional bytes of CRC data is added to raw packets to ensure integrity (0 through 4 allowed).
    /// </summary>
    /// <remarks>default = 0</remarks>
    public byte CrcBytes;

    /// <summary>
    /// Which encryption method is to be used (see enumeration) (this occurs at the raw packet level)
    /// </summary>
    /// <remarks>default = <see cref="EncryptMethod.None"/></remarks>
    public EncryptMethod[] EncryptMethod = new EncryptMethod[Constants.EncryptPasses];

    /// <summary>
    /// Constructor merely sets default values for the structure members.
    /// </summary>
    public UdpParams(ManagerRole role = ManagerRole.Default)
    {
        OutgoingBufferSize = 64 * 1024;
        IncomingBufferSize = 64 * 1024;
        PacketHistoryMax = 4;
        MaxDataHoldTime = 50;
        MaxDataHoldSize = -1;
        MaxRawPacketSize = 512;
        HashTableSize = 100;
        AvoidPriorityQueue = false;
        ClockSyncDelay = 0;
        CrcBytes = 0;
        EncryptMethod[0] = Enumerations.EncryptMethod.None;
        EncryptMethod[1] = Enumerations.EncryptMethod.None;
        KeepAliveDelay = 0;
        PortAliveDelay = 0;
        NoDataTimeout = 0;
        MaxConnections = 10;
        Port = 0;
        PortRange = 0;
        PooledPacketMax = 1000;
        PooledPacketSize = -1;
        PooledPacketInitial = 0;
        ReplyUnreachableConnection = true;
        AllowPortRemapping = true;
        AllowAddressRemapping = false;
        IcmpErrorRetryPeriod = 5000;
        OldestUnacknowledgedTimeout = 120000;
        ProcessIcmpErrors = true;
        ProcessIcmpErrorsDuringNegotiating = false;
        ConnectAttemptDelay = 1000;
        ReliableOverflowBytes = 0;
        LingerDelay = 10;
        BindIpAddress = string.Empty;
        UdpDriver = null;
        CallbackEventPoolMax = 5000;
        EventQueuing = false;
        ThreadSleepTime = 20;
        IncomingLogicalPacketMax = 20 * 1024 * 1024;
        ProtocolName = string.Empty;
        UserSuppliedEncryptExpansionBytes = 0;
        UserSuppliedEncryptExpansionBytes2 = 0;

        Reliable[0].MaxInstandingPackets = 400;
        Reliable[0].MaxOutstandingBytes = 200 * 1024;
        Reliable[0].MaxOutstandingPackets = 400;
        Reliable[0].OutOfOrder = false;
        Reliable[0].Coalesce = true;
        Reliable[0].AckDeduping = true;
        Reliable[0].FragmentSize = 0;
        Reliable[0].ResendDelayAdjust = 300;
        Reliable[0].ResendDelayPercent = 125;
        Reliable[0].ResendDelayCap = 8000;
        Reliable[0].ToleranceLossCount = 0;
        Reliable[0].CongestionWindowMinimum = 0;
        Reliable[0].CongestionWindowMaximum = 8 * 1024;
        Reliable[0].TrickleRate = 0;
        Reliable[0].TrickleSize = 0;

        switch (role)
        {
            case ManagerRole.InternalServer:
                OutgoingBufferSize = 4 * 1024 * 1024;
                IncomingBufferSize = 4 * 1024 * 1024;
                CrcBytes = 2;
                IcmpErrorRetryPeriod = 500;
                MaxRawPacketSize = 1460;
                HashTableSize = 10000;
                KeepAliveDelay = 30000;
                NoDataTimeout = 90000;
                MaxConnections = 2000;
                PooledPacketMax = 20000;
                PooledPacketInitial = 1000;
                AllowPortRemapping = false;
                Reliable[0].MaxInstandingPackets = 1000;
                Reliable[0].MaxOutstandingBytes = 1024 * 1024;
                Reliable[0].MaxOutstandingPackets = 1000;
                Reliable[0].CongestionWindowMinimum = 4 * 1024;
                Reliable[0].CongestionWindowMaximum = 16 * 1024;
                Reliable[0].ResendDelayAdjust = 150;
                break;

            case ManagerRole.InternalClient:
                OutgoingBufferSize = 1024 * 1024;
                IncomingBufferSize = 1024 * 1024;
                CrcBytes = 2;
                IcmpErrorRetryPeriod = 500;
                MaxRawPacketSize = 1460;
                HashTableSize = 10;
                KeepAliveDelay = 30000;
                NoDataTimeout = 90000;
                MaxConnections = 2;
                PooledPacketMax = 2000;
                PooledPacketInitial = 100;
                AllowPortRemapping = false;
                Reliable[0].MaxInstandingPackets = 1000;
                Reliable[0].MaxOutstandingBytes = 1024 * 1024;
                Reliable[0].MaxOutstandingPackets = 1000;
                Reliable[0].CongestionWindowMinimum = 4 * 1024;
                Reliable[0].CongestionWindowMaximum = 16 * 1024;
                Reliable[0].ResendDelayAdjust = 150;
                break;

            case ManagerRole.ExternalServer:
                OutgoingBufferSize = 2 * 1024 * 1024;
                IncomingBufferSize = 2 * 1024 * 1024;
                CrcBytes = 2;
                IcmpErrorRetryPeriod = 2500;
                HashTableSize = 10000;
                KeepAliveDelay = 30000;
                NoDataTimeout = 90000;
                MaxConnections = 2000;
                PooledPacketMax = 20000;
                PooledPacketInitial = 1000;
                break;

            case ManagerRole.ExternalClient:
                CrcBytes = 2;
                IcmpErrorRetryPeriod = 2500;
                HashTableSize = 10;
                KeepAliveDelay = 30000;
                NoDataTimeout = 90000;
                MaxConnections = 2;
                PooledPacketMax = 2000;
                PooledPacketInitial = 10;
                break;

            case ManagerRole.Lfn:
                OutgoingBufferSize = 16 * 1024 * 1024;
                IncomingBufferSize = 16 * 1024 * 1024;
                CrcBytes = 2;
                IcmpErrorRetryPeriod = 1500;
                MaxRawPacketSize = 1460;
                KeepAliveDelay = 30000;
                NoDataTimeout = 90000;
                MaxConnections = 2;
                PooledPacketMax = 50000;
                PooledPacketInitial = 5000;
                AllowPortRemapping = false;
                IncomingLogicalPacketMax = 200 * 1024 * 1024;
                CallbackEventPoolMax = 50000;
                Reliable[0].MaxInstandingPackets = 32000;
                Reliable[0].MaxOutstandingBytes = 50 * 1024 * 1024;
                Reliable[0].MaxOutstandingPackets = 32000;
                Reliable[0].CongestionWindowMinimum = 300000;
                Reliable[0].CongestionWindowMaximum = 300000;
                Reliable[0].ToleranceLossCount = 100;
                break;

            case ManagerRole.Default:
            default:
                break;
        }

        for (var i = 1; i < Constants.ReliableChannelCount; i++)
            Reliable[i] = Reliable[0];
    }
}