using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AbilityExperience : ISerializableType
{
    public int Unknown;

    public bool Unknown2;

    public int NameId;
    public int DescriptionId;

    public int IconId;

    public int Unknown6;
    public int Level;
    public int Progress;
    public int TotalForLevel;
    public int Unknown10;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);

        if (Unknown == 0)
            return;

        writer.Write(Unknown2);

        writer.Write(NameId);
        writer.Write(DescriptionId);

        writer.Write(IconId);

        writer.Write(Unknown6);

        writer.Write(Level);
        writer.Write(Progress);
        writer.Write(TotalForLevel);
        writer.Write(Unknown10);
    }
}