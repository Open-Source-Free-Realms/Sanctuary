using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ProfileItem : ISerializableType
{
    public int Id { get; set; }
    public int Slot { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Slot);
    }
}