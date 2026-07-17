using Sanctuary.Core.IO;

namespace Sanctuary.Packet;


public class PlayerUpdatePacketAddEffectTagCompositeEffect : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 41;

    public ulong Guid;
    public int TagId;
    public int CompositeEffectId;
    public ulong SourceGuid;
    private long Unused = default;

    public PlayerUpdatePacketAddEffectTagCompositeEffect() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(TagId);
        writer.Write(CompositeEffectId);
        writer.Write(SourceGuid);
        writer.Write(Unused);

        return writer.Buffer;
    }
}
