using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class BroadcastPacketZone : BaseBroadcastPacket, ISerializablePacket
{
    public new const short OpCode = 2;

    public NameData Name = new();

    public string? Message;

    public int MessageId;

    public BroadcastPacketZone() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        Name.Serialize(writer);

        writer.Write(Message);
        writer.Write(MessageId);

        return writer.Buffer;
    }
}
