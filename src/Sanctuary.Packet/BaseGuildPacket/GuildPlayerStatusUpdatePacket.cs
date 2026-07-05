using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildPlayerStatusUpdatePacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 31;

    public ulong PlayerGuid;
    public ulong GuildGuid;

    public bool IsInGuild;

    public GuildPlayerStatusUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);
        writer.Write(GuildGuid);

        writer.Write(IsInGuild);

        return writer.Buffer;
    }
}