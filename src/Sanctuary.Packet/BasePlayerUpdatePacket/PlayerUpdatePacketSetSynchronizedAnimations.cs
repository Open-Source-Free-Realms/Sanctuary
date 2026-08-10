using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Plays the same looping animation on every listed entity in sync.
public class PlayerUpdatePacketSetSynchronizedAnimations : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 63;

    public class Animation
    {
        public ulong Guid;
        public int AnimationId;
    }

    public List<Animation> Animations = new();

    public PlayerUpdatePacketSetSynchronizedAnimations() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Animations.Count);

        foreach (var animation in Animations)
        {
            writer.Write(animation.Guid);
            writer.Write(animation.AnimationId);
        }

        return writer.Buffer;
    }
}
