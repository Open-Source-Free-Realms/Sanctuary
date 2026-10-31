using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketQueueAnimation : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 22;

    public ulong Guid;
    public int AnimationGroupId;
    public int LoopCount;
    public float PlaybackSpeed;
    public float Delay;
    public float EndTime;
    public bool Unknown6;
    public bool Unknown7;
    public bool Unknown8;

    public PlayerUpdatePacketQueueAnimation() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(AnimationGroupId);
        writer.Write(LoopCount);
        writer.Write(PlaybackSpeed);
        writer.Write(Delay);
        writer.Write(EndTime);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Unknown8);

        return writer.Buffer;
    }
}
