using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CustomizationDetail : ISerializableType
{
    public int Type;

    public string? Unknown2;
    public string? Unknown3;

    public int Unknown4;

    public string? Unknown5;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Type);

        writer.Write(Unknown2);
        writer.Write(Unknown3);

        writer.Write(Unknown4);

        writer.Write(Unknown5);
    }
}