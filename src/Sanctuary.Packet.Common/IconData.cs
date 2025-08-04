using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class IconData
{
    public int Id { get; set; }
    public int TintId { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(TintId);
    }
}