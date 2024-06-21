using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ProfileItemClassData : ISerializableType
{
    public int Id { get; set; }
    public int Unknown { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Unknown);
    }
}