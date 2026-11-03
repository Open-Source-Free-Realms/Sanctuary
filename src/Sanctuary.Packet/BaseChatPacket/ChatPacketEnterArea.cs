using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ChatPacketEnterArea : BaseChatPacket, ISerializablePacket
{
    public new const short OpCode = 2;

    public int AreaNameId;

    public ChatPacketEnterArea() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(AreaNameId);

        return writer.Buffer;
    }
}