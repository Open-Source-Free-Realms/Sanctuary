using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op32 sub7 "AttackProcessed" — per-hit combat feedback.
//   ATTACKER (written twice on the wire; the client stores both copies into one field): plays the
//   attack-contact event — and if it's the LOCAL PLAYER, resets the action-bar melee cooldown timer,
//   so prefer PlayerUpdatePacketHitPointModification for the player's own hits.
//   TARGET: floating damage number (-Damage), health bar (CurrentHealth/MaxHealth), recoil, hit FX.
// Swapping attacker/target makes the victim swing and go on cooldown while the attacker takes the
// damage.
public class CombatPacketAttackProcessed : ISerializablePacket
{
    public const short OpCode = 32;
    public const short SubOpCode = 7;

    /// <summary>Who swings.</summary>
    public ulong AttackerGuid;

    /// <summary>Who takes the number / bar / recoil / hit FX.</summary>
    public ulong TargetGuid;

    /// <summary>Damage dealt — rendered as a floating -Damage.</summary>
    public int Damage;

    /// <summary>Target's max HP (health-bar denominator).</summary>
    public int MaxHealth;

    /// <summary>Hit composite effect id played on the target.</summary>
    public int CompositeEffectId;

    public bool Bool1;
    public bool Bool2;

    public int Int4;

    /// <summary>Target's CURRENT HP after this hit (bar position).</summary>
    public int CurrentHealth;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(AttackerGuid);
        writer.Write(AttackerGuid); // duplicated on the wire
        writer.Write(TargetGuid);

        writer.Write(Damage);
        writer.Write(MaxHealth);
        writer.Write(CompositeEffectId);

        writer.Write(Bool1);
        writer.Write(Bool2);

        writer.Write(Int4);
        writer.Write(CurrentHealth);

        return writer.Buffer;
    }
}
