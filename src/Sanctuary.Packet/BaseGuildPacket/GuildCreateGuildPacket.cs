using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildCreateGuildPacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 33;

    public bool CreateGuild;

    public GuildCreateGuildPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(CreateGuild);

        return writer.Buffer;
    }
}