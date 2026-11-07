using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ObjectiveCompletePacket : BaseObjectivePacket, ISerializablePacket, IDeserializable<ObjectiveCompletePacket>
{
    public new const byte OpCode = 3;

    public int ObjectiveId;

    public int CompositeEffectId;

    public int Points;

    public ObjectiveCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);
        writer.Write(CompositeEffectId);
        writer.Write(Points);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ObjectiveCompletePacket objectiveCompletePacket)
    {
        objectiveCompletePacket = new ObjectiveCompletePacket();

        var reader = new PacketReader(data);

        if (!objectiveCompletePacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out objectiveCompletePacket.ObjectiveId))
            return false;

        if (!reader.TryRead(out objectiveCompletePacket.CompositeEffectId))
            return false;

        if (!reader.TryRead(out objectiveCompletePacket.Points))
            return false;

        return reader.RemainingLength == 0;
    }
}
