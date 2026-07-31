using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildErrorPacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 23;

    public string? MessageName;

    public GuildErrorPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(MessageName);

        return writer.Buffer;
    }
}