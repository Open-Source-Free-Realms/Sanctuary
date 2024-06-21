using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerTitleRequestSelectPacket : BasePlayerTitlePacket, IDeserializable<PlayerTitleRequestSelectPacket>
{
    public new const short OpCode = 4;

    public int Id;

    public PlayerTitleRequestSelectPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PlayerTitleRequestSelectPacket value)
    {
        value = new PlayerTitleRequestSelectPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Id))
            return false;

        return reader.RemainingLength == 0;
    }
}