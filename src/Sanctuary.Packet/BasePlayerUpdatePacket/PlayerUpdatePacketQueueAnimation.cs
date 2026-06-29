using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketQueueAnimation : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 22;

    public ulong Guid;
    public int AnimationGroupId;
    public int Unknown;
    public float Unknown2;
    public float Unknown3;
    public float Unknown4;
    public bool Unknown5;
    public bool Unknown6;
    public bool Unknown7;

    public PlayerUpdatePacketQueueAnimation() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Guid);
        writer.Write(AnimationGroupId);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);

        return writer.Buffer;
    }
}
