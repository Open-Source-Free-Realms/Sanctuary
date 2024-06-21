namespace Sanctuary.UdpLibrary.Internal;

internal static class Constants
{
    internal const int EncryptPasses = 2;
    internal const int ReliableChannelCount = 4;

    /// <summary>
    /// Protocol version must match on both ends, or connect packets are simply ignored by the server.
    /// </summary>
    internal const int ProtocolVersion = 3;

    internal const int HardMaxRawPacketSize = 0x2000;

    /// <summary>
    /// Don't change this.
    /// </summary>
    internal const int HardMaxOutstandingPackets = 30000;

    internal const int UdpPacketOrderedSize = 4;
    internal const int UdpPacketReliableSize = 4;

    internal const int MaximumTimeAllowedForPortRemapping = 5000;
}