using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sent server→client to confirm damage dealt to a target (OpCode 32, SubOpCode 4).
/// </summary>
public class CombatPacketAttackTargetDamage : BaseCombatPacket, ISerializablePacket
{
    public new const short OpCode = 4;

    /// <summary>GUID of the attacker.</summary>
    public ulong AttackerGuid;

    /// <summary>GUID of the target being damaged.</summary>
    public ulong TargetGuid;

    /// <summary>Damage amount dealt.</summary>
    public int Damage;

    public CombatPacketAttackTargetDamage() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(AttackerGuid);
        writer.Write(TargetGuid);
        writer.Write(Damage);

        return writer.Buffer;
    }
}
