using System;

using Sanctuary.UdpLibrary.Enumerations;
using Sanctuary.UdpLibrary.Packets;

namespace Sanctuary.UdpLibrary.Internal;

internal class CallbackEvent : IDisposable
{
    public CallbackEventType EventType;
    public UdpConnection? Source;
    public LogicalPacket? Payload;

    /// <summary>
    /// Only used by <see cref="CallbackEventType.PacketCorrupt"/> event.
    /// </summary>
    public UdpCorruptionReason Reason;

    public CallbackEvent()
    {
        EventType = CallbackEventType.None;
        Source = null;
        Payload = null;
        Reason = UdpCorruptionReason.None;
    }

    public void SetEventData(CallbackEventType eventType, UdpConnection con, LogicalPacket? payload = null)
    {
        Source = con;
        Payload = payload;
        EventType = eventType;
    }

    public void ClearEventData()
    {
        Source = null;
        Payload = null;
    }

    public void Dispose()
    {
        ClearEventData();
    }
}