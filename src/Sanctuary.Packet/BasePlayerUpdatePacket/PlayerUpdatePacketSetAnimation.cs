using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketSetAnimation : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 8;

    public ulong Guid;
    public int AnimationId;
    public int Unknown;

    // Bit 0 set = set the entity's base/idle animation, otherwise play it now.
    public byte Flags;

    public PlayerUpdatePacketSetAnimation() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(AnimationId);
        writer.Write(Unknown);
        writer.Write(Flags);

        return writer.Buffer;
    }
}
