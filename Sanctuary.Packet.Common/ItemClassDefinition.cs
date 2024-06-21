using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ItemClassDefinition : ISerializableType
{
    public int Id;
    public int NameId;
    public int IconId;
    public int WieldType;
    public int StatId;
    public int ProfileNameId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(WieldType);
        writer.Write(StatId);
        writer.Write(ProfileNameId);
    }
}