using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// Sub-opcode 11: int SchoolId + Vector4 Position + Vector4 Rotation + int FishCount + FishItem[FishCount] + int + int
/// FishItem = 3 ints (last has bool flag in high byte)
public class FishingPacketSpawnProxiedFishingSchool : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 11;

    public int SchoolId;
    public Vector4 Position;
    public Vector4 Rotation;
    public List<FishingSchoolFishItem> Fish = [];
    public List<int> ModelIds = [];
    public int Unknown1;
    public int Unknown2;

    public FishingPacketSpawnProxiedFishingSchool() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(SchoolId);
        writer.Write(Position);
        writer.Write(Rotation);
        writer.Write(Fish.Count);
        foreach (var f in Fish)
        {
            writer.Write(f.ModelId);
            writer.Write(f.Unknown2);
            writer.Write(f.Unknown3);
        }
        writer.Write(ModelIds);
        writer.Write(Unknown1);
        writer.Write(Unknown2);
        return writer.Buffer;
    }
}

public class FishingSchoolFishItem
{
    public int ModelId;
    public int Unknown2;
    public int Unknown3;
}
