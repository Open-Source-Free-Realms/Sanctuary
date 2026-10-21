using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseMiniGamePacket : ISerializablePacket, IDeserializable<BaseMiniGamePacket>
{
    public const short OpCode = 39;

    private byte SubOpCode;

    public int StateId;

    public int GameId;

    public int GroupId;

    public BaseMiniGamePacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public virtual void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) || subOpCode != SubOpCode)
            return false;

        return true;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(StateId);
        writer.Write(GameId);
        writer.Write(GroupId);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out BaseMiniGamePacket baseMiniGamePacket)
    {
        baseMiniGamePacket = new(7);

        var reader = new PacketReader(data);

        if (!baseMiniGamePacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out baseMiniGamePacket.StateId))
            return false;

        if (!reader.TryRead(out baseMiniGamePacket.GameId))
            return false;

        if (!reader.TryRead(out baseMiniGamePacket.GroupId))
            return false;

        return reader.RemainingLength == 0;
    }
}
