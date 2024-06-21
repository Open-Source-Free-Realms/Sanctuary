using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PlayerCustomizationData : ISerializableType
{
    public int Id;
    public string? Unknown2;
    public int Unknown3;
    public int Unknown4;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
    }
}