using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op35 sub5 — per-entity hitpoints update (drives an NPC's nameplate health bar), unlike
// ClientUpdatePacketHitpoints (op38 sub1) which is self-only. The client reads all three ints;
// current/max order is the working hypothesis.
public class PlayerUpdatePacketUpdateHitpoints : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public ulong Guid;

    public int Hitpoints;
    public int MaxHitpoints;
    public int Unknown;

    public PlayerUpdatePacketUpdateHitpoints() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(Hitpoints);
        writer.Write(MaxHitpoints);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
