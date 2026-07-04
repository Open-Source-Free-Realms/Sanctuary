using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ChatPacketDebugChat : BaseChatPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public string? Message = null!;

    public bool PrintToChat;

    public ChatPacketDebugChat() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Message);

        writer.Write(PrintToChat);

        return writer.Buffer;
    }
}