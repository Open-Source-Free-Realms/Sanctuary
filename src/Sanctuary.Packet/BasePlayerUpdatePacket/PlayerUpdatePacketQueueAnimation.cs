using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketQueueAnimation : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 22;

    public ulong Guid;
    public int AnimationId;
    public int Unknown3;
    public float Speed = 1.0f;
    public int Unknown5;
    public int Unknown6;
    public bool Unknown7;
    public bool Interrupt = true;
    public bool Unknown9;

    public PlayerUpdatePacketQueueAnimation() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(AnimationId);
        writer.Write(Unknown3);
        writer.Write(Speed);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Interrupt);
        writer.Write(Unknown9);

        return writer.Buffer;
    }
}
