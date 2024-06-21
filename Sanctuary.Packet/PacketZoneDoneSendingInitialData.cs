using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketZoneDoneSendingInitialData : ISerializablePacket
{
    public const short OpCode = 14;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        return writer.Buffer;
    }
}