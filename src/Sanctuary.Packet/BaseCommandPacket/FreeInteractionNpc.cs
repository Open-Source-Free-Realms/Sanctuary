using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class FreeInteractionNpc : BaseCommandPacket, IDeserializable<FreeInteractionNpc>
{
    public new const short OpCode = 20;

    public FreeInteractionNpc() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FreeInteractionNpc value)
    {
        value = new FreeInteractionNpc();
        var reader = new PacketReader(data);
        return value.TryRead(ref reader) && reader.RemainingLength == 0;
    }
}
