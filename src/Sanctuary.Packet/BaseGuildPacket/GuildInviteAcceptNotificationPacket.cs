using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GuildInviteAcceptNotificationPacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 27;

    public ulong PlayerGuid;
    public NameData Name = new();

    public GuildInviteAcceptNotificationPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);
        Name.Serialize(writer);

        return writer.Buffer;
    }
}
