using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GuildDataFullPacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 22;

    public ulong Guid;

    public GuildData Data = new();

    public GuildDataFullPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        Data.Serialize(writer);

        return writer.Buffer;
    }
}