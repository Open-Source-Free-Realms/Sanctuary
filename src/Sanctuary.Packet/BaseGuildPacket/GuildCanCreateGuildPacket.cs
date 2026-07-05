using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildCanCreateGuildPacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 32;

    public bool CanCreateGuild;

    public GuildCanCreateGuildPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(CanCreateGuild);

        return writer.Buffer;
    }
}