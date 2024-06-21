using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class IconData
{
    public int Id;
    public int TintId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(TintId);
    }
}