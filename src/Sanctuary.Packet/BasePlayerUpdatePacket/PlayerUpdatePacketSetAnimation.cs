using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Server -> client "play an animation on an entity" (opcode 35 / sub-opcode 8). Reverse-engineered from
/// the client's PlayerUpdate dispatcher (FUN_0092f460 case 8) -> deserializer FUN_00908a50 -> field reader
/// FUN_008e5dd0. The client resolves <see cref="Guid"/> to the entity and drives its animation mixer
/// (FUN_0096c780 - logs "play anim %d", applies to the "upperbody"/full body).
///
/// Wire (after the 4-byte header short OpCode(35) + short SubOpCode(8)):
///   ulong Guid       - target entity (client field +0x10/+0x14)
///   int   AnimationId - the animation to play (+0x18); the "%d" in the client's "play anim %d"
///   int   Unknown1c   - secondary param (+0x1c); blend/duration-ish, 0 works
///   byte  Flags       - (+0x20) bit0: 1 = set the entity's base/idle anim (stored at entity+0x51c),
///                       0 = play the animation now. Use 0 to play a one-shot/looping animation.
/// </summary>
public class PlayerUpdatePacketSetAnimation : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 8;

    public ulong Guid;
    public int AnimationId;
    public int Unknown1c;
    public byte Flags;

    public PlayerUpdatePacketSetAnimation() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer); // opcode 35 + sub-opcode 8

        writer.Write(Guid);
        writer.Write(AnimationId);
        writer.Write(Unknown1c);
        writer.Write(Flags);

        return writer.Buffer;
    }
}
