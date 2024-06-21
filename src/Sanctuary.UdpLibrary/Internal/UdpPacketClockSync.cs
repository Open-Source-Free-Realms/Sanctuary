namespace Sanctuary.UdpLibrary.Internal;

internal struct UdpPacketClockSync
{
    internal ushort TimeStamp;
    internal uint MasterPingTime;
    internal uint AveragePingTime;
    internal uint LowPingTime;
    internal uint HighPingTime;
    internal uint LastPingTime;
    internal long OurSent;
    internal long OurReceived;
}