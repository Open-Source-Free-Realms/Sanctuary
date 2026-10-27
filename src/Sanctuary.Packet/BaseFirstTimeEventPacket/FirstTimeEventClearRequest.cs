using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sent when the client asks to clear first-time events.
/// The layout is unconfirmed: the client is assumed to send a single FirstTimeEvents.txt id.
/// <see cref="Payload"/> keeps the raw body so the layout can be verified from logs.
/// </summary>
public class FirstTimeEventClearRequest : BaseFirstTimeEventPacket, IDeserializable<FirstTimeEventClearRequest>
{
    public new const byte OpCode = 3;

    public int? Id;

    public byte[] Payload = Array.Empty<byte>();

    public FirstTimeEventClearRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FirstTimeEventClearRequest value)
    {
        value = new FirstTimeEventClearRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        value.Payload = reader.RemainingSpan.ToArray();

        if (reader.RemainingLength == sizeof(int) && reader.TryRead(out int id))
            value.Id = id;

        return true;
    }
}
