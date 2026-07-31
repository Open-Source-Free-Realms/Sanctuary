using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op35 sub41 "AddEffectTagCompositeEffect" — attach a LOOPING composite effect to a character as part
// of a status-effect tag (the visible half of a buff/debuff). GROUND TRUTH (2014-04-01 capture idx
// 37215, the heart pickup): when the player grabbed the health powerup the server attached
// composite 15921 (PFX_magic-heal_red_head_shower_lg_loop_raised = the looping heal shower over the
// head + trail) to the player, keyed by tag id, sourced from the heart's guid.
//   wire (36B): [short 35][short 41][ulong Guid][int TagId][int CompositeEffectId][ulong SourceGuid]
//               [int 0][int 0]
// Pair with PlayerUpdatePacketRemoveEffectTagCompositeEffect (sub42) to stop the loop.
public class PlayerUpdatePacketAddEffectTagCompositeEffect : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 41;

    public ulong Guid;
    public int TagId;
    public int CompositeEffectId;
    public ulong SourceGuid;
    public int Unknown;
    public int Unknown2;

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
        writer.Write(Unknown);
        writer.Write(Unknown2);

        return writer.Buffer;
    }
}
