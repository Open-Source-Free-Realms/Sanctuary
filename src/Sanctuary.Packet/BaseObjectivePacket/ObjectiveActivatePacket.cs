using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ObjectiveActivatePacket : BaseObjectivePacket, ISerializablePacket, IDeserializable<ObjectiveActivatePacket>
{
    public new const byte OpCode = 1;

    public int ObjectiveId;

    public int Unknown;

    public ObjectiveActivatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);
        writer.Write(Unknown);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ObjectiveActivatePacket objectiveActivatePacket)
    {
        objectiveActivatePacket = new ObjectiveActivatePacket();

        var reader = new PacketReader(data);

        if (!objectiveActivatePacket.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out objectiveActivatePacket.ObjectiveId))
            return false;

        if (!reader.TryRead(out objectiveActivatePacket.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}
