using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ObjectiveCompletePacket : BaseObjectivePacket, ISerializablePacket, IDeserializable<ObjectiveCompletePacket>
{
    public new const byte OpCode = 3;

    public int ObjectiveId;

    public int Unknown;

    public int Unknown2;

    public ObjectiveCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);
        writer.Write(Unknown);
        writer.Write(Unknown2);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ObjectiveCompletePacket objectiveCompletePacket)
    {
        objectiveCompletePacket = new ObjectiveCompletePacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseObjectivePacket.OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out objectiveCompletePacket.ObjectiveId))
            return false;

        if (!reader.TryRead(out objectiveCompletePacket.Unknown))
            return false;

        if (!reader.TryRead(out objectiveCompletePacket.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}
