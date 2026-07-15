using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op35 sub35 — the floating combat damage/heal number over an entity, plus its health-bar update.
// Guid = attacker, Guid2 = victim, Unknown2 = max HP, Unknown3 = current HP after the hit,
// Unknown4 = the delta (-damage — the floating number).
// Unlike CombatPacketAttackProcessed with attacker == local player, this does NOT reset the client's
// action-bar melee timer, so it's the correct vehicle for the player's own hits.
public class PlayerUpdatePacketHitPointModification : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 35;

    public ulong Guid;
    public ulong Guid2;

    public bool Unknown;

    public int Unknown2;
    public int Unknown3;
    public int Unknown4;

    public bool Unknown5;

    public PlayerUpdatePacketHitPointModification() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Guid2);

        writer.Write(Unknown);

        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);

        writer.Write(Unknown5);

        return writer.Buffer;
    }
}
