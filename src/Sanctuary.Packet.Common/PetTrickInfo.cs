using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PetTrickInfo : ISerializableType
{
    public int Id;

    public int IconId;

    public int NameId;

    public int SkillLevel;

    public int Unknown5;

    public int Unknown6;

    public int TooltipStringId;

    public int Unknown8;

    public bool Known;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(IconId);
        writer.Write(NameId);
        writer.Write(SkillLevel);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(TooltipStringId);
        writer.Write(Unknown8);
        writer.Write(Known);
    }
}
