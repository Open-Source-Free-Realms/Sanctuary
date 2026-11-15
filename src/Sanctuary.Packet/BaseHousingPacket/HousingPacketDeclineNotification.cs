using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketDeclineNotification : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 48;

    public ulong Guid;
    public ulong Guid2;

    public NameData Name = new();

    public HousingPacketDeclineNotification() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Guid2);

        Name.Serialize(writer);

        return writer.Buffer;
    }
}