namespace Sanctuary.UdpLibrary.Internal;

internal struct UdpPacketClockReflect
{
    internal ushort TimeStamp;
    internal uint ServerSyncStampLong;
    internal long YourSent;
    internal long YourReceived;
    internal long OurSent;
    internal long OurReceived;
}