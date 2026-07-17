using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketRemoveEffectTagCompositeEffect : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 42;

    public ulong Guid;
    public int TagId;

    public PlayerUpdatePacketRemoveEffectTagCompositeEffect() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(TagId);

        return writer.Buffer;
    }
}
