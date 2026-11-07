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
    public bool EaseOutLoopingAnimations;
    public bool UseTransitions;
    public bool UseAnimationPriority;

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
        writer.Write(EaseOutLoopingAnimations);
        writer.Write(UseTransitions);
        writer.Write(UseAnimationPriority);

        return writer.Buffer;
    }
}
