using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class FishingZoneConfig : ISerializableType
{
    // Client reads Unknown3 and Unknown6 as floats (fUnknown3 = fish-run X baseline,
    // fUnknown6 -> m_fUnknownFloat = water-surface Y used for bobber/fish placement).
    public float Unknown6;

    public string? Unknown;

    public int Unknown2;
    public float Unknown3;
    public int Unknown4;
    public int Unknown5;

    // public List<FishingSchoolInstanceDefinition> FishingSchoolInstances = [];
    // public Dictionary<int, FishingSchoolPathDefinition> FishingSchoolPaths = [];

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);

        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
    }
}