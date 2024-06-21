using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Targets;

public class TargetLocation : TargetBase
{
    public Vector4 Unknown2 { get; }
    public Vector4 Unknown3 { get; }

    public TargetLocation(Vector4 unknown2, Vector4 unknown3)
    {
        Unknown2 = unknown2;
        Unknown3 = unknown3;
    }

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(Unknown2);
        writer.Write(Unknown3);
    }
}