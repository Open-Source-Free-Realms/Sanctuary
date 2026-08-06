using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GuildInviteDeclineNotificationPacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 28;

    public NameData Name = new();

    public bool TimedOut;

    public GuildInviteDeclineNotificationPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Name.Serialize(writer);

        writer.Write(TimedOut);

        return writer.Buffer;
    }
}
