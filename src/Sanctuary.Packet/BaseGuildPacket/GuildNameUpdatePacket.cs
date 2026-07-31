using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildNameUpdatePacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 18;

    public ulong Guid;
    public string? Name;

    public GuildNameUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Name);

        return writer.Buffer;
    }
}