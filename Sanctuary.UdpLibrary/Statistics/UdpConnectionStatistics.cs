using Sanctuary.UdpLibrary.Configuration;

namespace Sanctuary.UdpLibrary.Statistics;

public struct UdpConnectionStatistics
{
    /// These statistics are valid even if clock-sync is not used.
    /// These statistics are never reset and should not be as the negotiated
    /// packetloss stats would get messed up if they were
    /// as such, use <see cref="UdpConnection{TConnection}.ConnectionAge"/> to determine how long they have been accumulating.

    public long TotalBytesSent;
    public long TotalBytesReceived;

    /// <summary>
    /// Total packets we have sent.
    /// </summary>
    public long TotalPacketsSent;

    /// <summary>
    /// Total packets we have received.
    /// </summary>
    public long TotalPacketsReceived;

    /// <summary>
    /// Total packets on our connection that have been rejected due to a crc error.
    /// </summary>
    public long CrcRejectedPackets;

    /// <summary>
    /// Total packets on our connection that have been rejected due to an order error (only applicable for ordered channel).
    /// </summary>
    public long OrderRejectedPackets;

    /// <summary>
    /// Total reliable packets that we received where we had already received it before and threw it away.
    /// </summary>
    public long DuplicatePacketsReceived;

    /// <summary>
    /// Number of times we have resent a packet due to receiving a later packet in the series.
    /// </summary>
    public long ResentPacketsAccelerated;

    /// <summary>
    /// Number of times we have resent a packet due to the ack-timeout expiring.
    /// </summary>
    public long ResentPacketsTimedOut;

    public long ApplicationPacketsSent;
    public long ApplicationPacketsReceived;

    /// <summary>
    /// Number of times this connection has been given processing time.
    /// </summary>
    public long Iterations;

    /// <summary>
    /// Number of misformed/corrupt packets.
    /// </summary>
    public long CorruptPacketErrors;

    /// These statistics are only valid if clock-sync'ing is enabled (highly recommended) (will be valid on both client and server side).
    /// These statistics are reset by <see cref="UdpConnection{TConnection}.PingStatReset"/> and are negotiated periodically by the clock-sync stuff <see cref="UdpParams.ClockSyncDelay"/>.

    /// <summary>
    /// Only valid (and applicable) on client side.
    /// </summary>
    public long MasterPingAge;

    public uint MasterPingTime;
    public uint AveragePingTime;
    public uint LowPingTime;
    public uint HighPingTime;
    public uint LastPingTime;

    /// <summary>
    /// The average time (over last 3 acks) for a reliable packet to get acked (when packet is not lost).
    /// </summary>
    public long ReliableAveragePing;

    /// <summary>
    /// Total packets we have sent at time they reported their numbers.
    /// </summary>
    public long SyncOurSent;

    /// <summary>
    /// Total packets we have received at time they reported their numbers.
    /// </summary>
    public long SyncOurReceived;

    /// <summary>
    /// Total packets they have sent.
    /// </summary>
    public long SyncTheirSent;

    /// <summary>
    /// Total packets they have received.
    /// </summary>
    public long SyncTheirReceived;

    public float PercentSentSuccess;
    public float PercentReceivedSuccess;
}