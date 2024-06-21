namespace Sanctuary.UdpLibrary.Configuration;

public struct UdpReliableConfig
{
    /// <summary>
    /// Maximum number of bytes that are allowed to be outstanding without an acknowledgement before more are sent.
    /// </summary>
    /// <value>200000</value>
    public int MaxOutstandingBytes = 200000;

    /// <summary>
    /// Maximum number of physical reliable packets that are allowed to be outstanding.
    /// </summary>
    /// <remarks>default = 400</remarks>
    public int MaxOutstandingPackets = 400;

    /// <summary>
    /// Maximum number of incoming reliable packets it will queue for ordered delivery while waiting for the missing packet to arrive (should generally be same as <see cref="MaxOutstandingPackets"/> setting on other side).
    /// </summary>
    /// <remarks>default = 400</remarks>
    public int MaxInstandingPackets = 400;

    /// <summary>
    /// This is the size it should fragment large logical packets into.
    /// </summary>
    /// <remarks>default = 0 max allowed = <see cref="UdpParams.MaxRawPacketSize"/></remarks>
    public int FragmentSize = 0;

    /// <summary>
    /// Maximum number of bytes to send per trickleRate period of time.
    /// </summary>
    /// <remarks>default = 0 max allowed = <see cref="FragmentSize"/></remarks>
    public int TrickleSize;

    /// <summary>
    /// How often <see cref="TrickleSize"/> bytes are sent on the channel.
    /// </summary>
    /// <remarks>default = 0 no trickle control</remarks>
    public int TrickleRate = 0;

    /// <summary>
    /// Amount of additional time (in ms) above the average ack-time before a packet should be deemed lost and resent.
    /// </summary>
    /// <remarks>default = 300</remarks>
    public int ResendDelayAdjust = 300;

    /// <summary>
    /// Percent average ack-time it should use in calculating the resend delay.
    /// </summary>
    /// <remarks>default = 125 or 125%</remarks>
    public int ResendDelayPercent = 125;

    /// <summary>
    /// Maximum length of resend-delay that will ever be assigned to an outstanding packet.
    /// </summary>
    /// <remarks>default = 5000</remarks>
    public int ResendDelayCap = 5000;

    /// <summary>
    /// The minimum size to allow the congestion-window to shrink.
    /// </summary>
    /// <remarks>
    /// This defaults to 0, though internally it the implementation will never let the window get smaller than a single raw packet (512 bytes by default).
    /// This setting is more intended to allow the application to set a higher minimum than that, effectively
    /// allowing the application to tell the connection to refuse to slow itself down as much.
    /// </remarks>
    public int CongestionWindowMinimum = 0;

    /// <summary>
    /// </summary>
    /// <remarks></remarks>
    public int CongestionWindowMaximum;

    /// <summary>
    /// The number of resend-accellerated packets in a frame that is severe enough to constitute a resetting of the flow control window.
    /// </summary>
    /// <remarks>
    /// Typically this number is set in extremely high bandwidth (LFN) situations where some small amount of packetloss should not reset the flow-control window.
    /// default = 0, which means that even is a single lost packet will reset the window, which matches previously UdpLibrary and TCP like behavior.
    /// Setting this too high can cause it to be unfriendly to other connections sharing the network.
    /// </remarks>
    public int ToleranceLossCount = 0;

    /// <summary>
    /// Whether incoming packets on this channel should be allowed to be delivered out of order.
    /// </summary>
    /// <remarks>default = false</remarks>
    public bool OutOfOrder = false;

    /// <summary>
    /// Whether the reliable-channel should attempt to coalesce data to reduce ack's needed.
    /// </summary>
    /// <remarks>
    /// default = true
    /// Rarely change this to false.
    /// </remarks>
    public bool Coalesce = true;

    /// <summary>
    /// Whether ack-packets stuck into the low-level multi-buffer should be deduped.
    /// </summary>
    /// <remarks>
    /// default = true
    /// Rarely change this to false.
    /// </remarks>
    public bool AckDeduping = true;

    public UdpReliableConfig()
    {
    }
}