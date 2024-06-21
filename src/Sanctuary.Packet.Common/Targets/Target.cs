using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Targets;

public class Target : ISerializableType, IDeserializable<Target>
{
    public TargetType Type;

    public TargetBase? TargetBase;

    protected Target()
    {
    }

    public static Target CreateTargetLocation(Vector4 unknown2, Vector4 unknown3)
    {
        return new Target
        {
            Type = TargetType.Location,
            TargetBase = new TargetLocation(unknown2, unknown3)
        };
    }

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Type);

        TargetBase?.Serialize(writer);
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out Target value)
    {
        value = new Target();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out value.Type))
            return false;

        // TODO

        return true;
    }
}