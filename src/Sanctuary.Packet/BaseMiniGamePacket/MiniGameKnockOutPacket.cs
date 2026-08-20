using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op39 sub23 — knockout counter/limit. Drives the combat-minigame HUD's knockout display, which
// renders MaxKnockouts - CurrentKnockouts as "remaining". Base ids are -1: the counter is
// whole-team, not per-state.
public class MiniGameKnockOutPacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 23;

    public int CurrentKnockouts;
    public int MaxKnockouts;

    public MiniGameKnockOutPacket(int currentKnockouts, int maxKnockouts,
        int stateId = -1, int groupId = -1, int gameId = -1)
        : base(OpCode, stateId, groupId, gameId)
    {
        CurrentKnockouts = currentKnockouts;
        MaxKnockouts = maxKnockouts;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(CurrentKnockouts);
        writer.Write(MaxKnockouts);

        return writer.Buffer;
    }
}
