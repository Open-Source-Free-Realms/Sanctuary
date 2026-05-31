using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GuildInviteNotificationPacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 24;

    public GuildInvite GuildInvite = new();

    public string? GuildName;

    public GuildInviteNotificationPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        GuildInvite.Serialize(writer);

        writer.Write(GuildName);

        return writer.Buffer;
    }
}