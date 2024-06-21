using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ForceDisconnect : ISerializablePacket
{
    public const byte OpCode = 4;

    public int Reason;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Reason);

        return writer.Buffer;
    }
}