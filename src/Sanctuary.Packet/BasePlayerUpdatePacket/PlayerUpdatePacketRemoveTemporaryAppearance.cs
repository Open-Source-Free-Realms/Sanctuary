using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketRemoveTemporaryAppearance : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 15;

    public ulong Guid;

    private int Unused = default;

    public PlayerUpdatePacketRemoveTemporaryAppearance() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(Unused);

        return writer.Buffer;
    }
}
