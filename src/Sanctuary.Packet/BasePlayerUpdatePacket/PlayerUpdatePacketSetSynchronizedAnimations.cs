using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Server -> client "play the same animation on several entities, in sync" (opcode 35 / sub-opcode 63).
/// Reverse-engineered from the client's PlayerUpdate dispatcher (FUN_0092f460 case 0x3f) -> deserializer
/// FUN_00919c90 -> list reader FUN_008fc2d0 -> element reader FUN_008db2a0. The client reads the whole
/// list, then for each entry resolves the guid and drives its animation mixer via the same apply used by
/// SetAnimation (FUN_0096c780) with duration -1, i.e. a looping animation. Because every listed entity is
/// started from one packet, the group stays phase-locked (a true synchronized dance).
///
/// Wire (after the 4-byte header short OpCode(35) + short SubOpCode(63)):
///   int Count
///   Count x { ulong Guid + int AnimationId }   (12 bytes each; element reader FUN_008db2a0)
/// </summary>
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

        base.Write(writer); // opcode 35 + sub-opcode 63

        writer.Write(Animations.Count);

        foreach (var animation in Animations)
        {
            writer.Write(animation.Guid);
            writer.Write(animation.AnimationId);
        }

        return writer.Buffer;
    }
}
