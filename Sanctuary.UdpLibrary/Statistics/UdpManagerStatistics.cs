namespace Sanctuary.UdpLibrary.Statistics;

public struct UdpManagerStatistics
{
    public long BytesSent;
    public long PacketsSent;
    public long BytesReceived;
    public long PacketsReceived;
    public long ConnectionRequests;
    public long CrcRejectedPackets;
    public long OrderRejectedPackets;
    public long DuplicatePacketsReceived;

    /// <summary>
    /// number of times we have resent a packet due to receiving a later packet in the series
    /// </summary>
    public long ResentPacketsAccelerated;

    /// <summary>
    /// number of times we have resent a packet due to the ack-timeout expiring
    /// </summary>
    public long ResentPacketsTimedOut;

    /// <summary>
    /// cumulative number of times a priority-queue entry has received processing time
    /// </summary>
    public long PriorityQueueProcessed;

    /// <summary>
    /// cumulative number of priority-queue entries that could have received processing time
    /// </summary>
    public long PriorityQueuePossible;
    public long ApplicationPacketsSent;
    public long ApplicationPacketsReceived;

    /// <summary>
    /// number of times GiveTime has been called
    /// </summary>
    public long Iterations;

    /// <summary>
    /// number of mis formed/corrupt packets
    /// </summary>
    public long CorruptPacketErrors;

    /// <summary>
    /// number of times the socket buffer was full when a send was attempted.
    /// </summary>
    public long SocketOverflowErrors;

    /// <summary>
    /// number of times GiveTime has aborted due to time, before exhausting all data in the socket buffer
    /// </summary>
    public long MaxPollingTimeExceeded;

    /// <summary>
    /// number of times DeliverEvents has aborted due to time, before exhausting all data in the event queue
    /// </summary>
    public long MaxDeliveryTimeExceeded;

    /// <summary>
    /// number of connections currently being managed
    /// </summary>
    public int ConnectionCount;

    /// <summary>
    /// number of connections that are pending disconnection
    /// </summary>
    public int DisconnectPendingCount;

    /// <summary>
    /// number of events that are in the call back event queue
    /// </summary>
    public int EventListCount;

    /// <summary>
    /// number of bytes of packet data that are in the call back event queue
    /// </summary>
    public int EventListBytes;

    /// <summary>
    /// number of packets created in the pool
    /// </summary>
    public int PoolCreated;

    /// <summary>
    /// number of packets available in the pool
    /// </summary>
    public int PoolAvailable;

    /// <summary>
    /// how long these statistics have been gathered (in milliseconds), useful for figuring out averages
    /// </summary>
    public UdpClockStamp ElapsedTime;

    public void Reset()
    {
        BytesSent = 0;
        PacketsSent = 0;
        BytesReceived = 0;
        PacketsReceived = 0;
        ConnectionRequests = 0;
        CrcRejectedPackets = 0;
        OrderRejectedPackets = 0;
        DuplicatePacketsReceived = 0;
        ResentPacketsAccelerated = 0;
        ResentPacketsTimedOut = 0;
        PriorityQueueProcessed = 0;
        PriorityQueuePossible = 0;
        ApplicationPacketsSent = 0;
        ApplicationPacketsReceived = 0;
        Iterations = 0;
        CorruptPacketErrors = 0;
        SocketOverflowErrors = 0;
        MaxPollingTimeExceeded = 0;
        MaxDeliveryTimeExceeded = 0;
        ConnectionCount = 0;
        DisconnectPendingCount = 0;
        EventListCount = 0;
        EventListBytes = 0;
        PoolCreated = 0;
        PoolAvailable = 0;
        ElapsedTime = 0;
    }
}