namespace Sanctuary.UdpLibrary.Enumerations;

internal enum CallbackEventType
{
    None,
    RoutePacket,
    ConnectComplete,
    Terminated,
    CrcReject,
    PacketCorrupt,
    ConnectRequest
}