using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op39 sub18 — the minigame/encounter ended and whether the player WON. This is what flips the
// end-of-game card between the win presentation and "TRY AGAIN!" — without it the state's Won flag
// stays false and the failure card shows even after a victory. StateId -1 = matches every state.
public class MiniGameGameOverPacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 18;

    public bool Won;
    public int Unknown1;      // client default 0
    public int Unknown2;      // client default 0
    public int Unknown3 = 1;  // client default 1

    public MiniGameGameOverPacket(bool won, int stateId = -1, int groupId = -1, int gameId = -1)
        : base(OpCode, stateId, groupId, gameId)
    {
        Won = won;
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Won);
        writer.Write(Unknown1);
        writer.Write(Unknown2);
        writer.Write(Unknown3);

        return writer.Buffer;
    }
}
