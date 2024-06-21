using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PlayerTitleData : ISerializableType
{
    public int Id { get; set; }
    public int Type { get; set; }
    public int NameId { get; set; }
    public int IconId { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Type);
        writer.Write(NameId);
        writer.Write(IconId);
    }
}