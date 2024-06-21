using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientProfileData : ISerializableType
{
    public int Id { get; set; }
    public int NameId { get; set; }
    public int DescriptionId { get; set; }
    public int Type { get; set; }
    public int Icon { get; set; }
    public int BadgeImageSet { get; set; }
    public int ButtonImageSet { get; set; }
    public int AbilityBgImageSet { get; set; }
    public int Hint { get; set; }
    public bool MembersOnly { get; set; }
    public bool Trial { get; set; }
    public int Unknown { get; set; }
    public int Unknown2 { get; set; }

    public Dictionary<int, ProfileItemClassData> ItemClasses { get; set; } = new();

    public List<int> DefaultItems { get; set; } = new();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Type);
        writer.Write(Icon);
        writer.Write(BadgeImageSet);
        writer.Write(ButtonImageSet);
        writer.Write(AbilityBgImageSet);
        writer.Write(Hint);
        writer.Write(MembersOnly);
        writer.Write(Trial);
        writer.Write(Unknown);
        writer.Write(Unknown2);

        writer.Write(ItemClasses);
    }
}