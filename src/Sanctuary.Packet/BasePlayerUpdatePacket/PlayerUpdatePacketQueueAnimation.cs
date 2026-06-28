using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketQueueAnimation : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 22;

    public ulong Guid;
    public int AnimationId;
    public bool Unknown1;

    public PlayerUpdatePacketQueueAnimation() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Guid);
        writer.Write(AnimationId);
        writer.Write(Unknown1);

        return writer.Buffer;
    }
}
