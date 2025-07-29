using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ItemCategoryDefinition : ISerializableType
{
    public int Id;
    public int NameId;
    public int IconId;
    public int Unknown;
    public bool Unknown2;

    /// <summary>
    /// 0 - Hidden
    /// X - Visible
    /// </summary>
    public int Display;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Display);
    }
}