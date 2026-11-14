using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketInviteNotification : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 47;

    public PendingHouseInvite Invite = new();

    public ulong Unknown;

    public HousingPacketInviteNotification() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Invite.Serialize(writer);

        writer.Write(Unknown);

        return writer.Buffer;
    }
}