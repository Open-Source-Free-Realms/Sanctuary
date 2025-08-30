using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class StopInspectPacket : BaseInspectPacket, IDeserializable<StopInspectPacket>
{
    public new const byte OpCode = 2;

    public StopInspectPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out StopInspectPacket value)
    {
        value = new StopInspectPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}