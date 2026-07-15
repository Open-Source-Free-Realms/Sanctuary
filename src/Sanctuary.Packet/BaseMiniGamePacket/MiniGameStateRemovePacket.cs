using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op39 sub19 — server-driven minigame state removal. Performs the full client-side teardown that
// otherwise only happens when the player clicks leave: hides the minigame UI, removes the
// MiniGameState, and (for combat-type games) exits combat mode. Without this the client stays "in
// the game" forever after an encounter ends. StateId <= 0 targets the first state.
public class MiniGameStateRemovePacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 19;

    public MiniGameStateRemovePacket(int stateId = 0, int groupId = -1, int gameId = -1)
        : base(OpCode, stateId, groupId, gameId)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }
}
