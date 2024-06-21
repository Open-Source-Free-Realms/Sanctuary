using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketClientGameSettings : ISerializablePacket
{
    public const short OpCode = 144;

    public int Unknown;
    public int Unknown2;
    public int Unknown3;

    public bool Unknown4;

    public float GameTimeScalar = 1.0f;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);

        writer.Write(Unknown4);

        writer.Write(GameTimeScalar);

        return writer.Buffer;
    }
}