using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketRequestStripEffect : BasePlayerUpdatePacket, IDeserializable<PlayerUpdatePacketRequestStripEffect>
{
    public new const short OpCode = 33;

    public int TagId;

    public PlayerUpdatePacketRequestStripEffect() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PlayerUpdatePacketRequestStripEffect value)
    {
        value = new PlayerUpdatePacketRequestStripEffect();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.TagId))
            return false;

        return reader.RemainingLength == 0;
    }
}
