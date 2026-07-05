using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// BaseCombatPacket op32/sub7 sends the main combat feedback update.
// Wire format from UnserializePacket sub_A2A910: after the op/sub header it reads
//   ulong, ulong, ulong, int, int, int, bool, bool, int, int = 46 bytes.
// Field semantics are still provisional:
//   Guid1 = attacker, Guid2 = target, Guid3 = ? ; Int1 = damage, Int2 = maxHP, Int3 = compositeEffectId;
//   Bool1/Bool2 = flags (crit/death?) ; Int4/Int5 = trailing.
public class CombatPacketAttackProcessed : ISerializablePacket
{
    public const short OpCode = 32;
    public const short SubOpCode = 7;

    public ulong Guid1;   // attacker
    public ulong Guid2;   // target
    public ulong Guid3;

    public int Int1;      // damage
    public int Int2;      // max health
    public int Int3;      // composite effect id

    public bool Bool1;
    public bool Bool2;

    public int Int4;
    public int Int5;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(Guid1);
        writer.Write(Guid2);
        writer.Write(Guid3);

        writer.Write(Int1);
        writer.Write(Int2);
        writer.Write(Int3);

        writer.Write(Bool1);
        writer.Write(Bool2);

        writer.Write(Int4);
        writer.Write(Int5);

        return writer.Buffer;
    }
}
