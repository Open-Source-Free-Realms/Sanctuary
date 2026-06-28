using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketRequestStripEffect : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 33;

    public ulong Guid;
    public int CompositeEffectId;

    public PlayerUpdatePacketRequestStripEffect() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Guid);
        writer.Write(CompositeEffectId);

        return writer.Buffer;
    }
}
