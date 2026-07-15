using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op39 — the minigame lifecycle family. Header: [short op39][byte subOpCode][int StateId][int GroupId]
// [int GameId]. Note the sub-opcode is a single BYTE (op41's is a short).
public class BaseMiniGamePacket
{
    public const short OpCode = 39;

    private byte SubOpCode;

    public int StateId;
    public int GroupId;
    public int GameId;

    public BaseMiniGamePacket(byte subOpCode, int stateId, int groupId, int gameId)
    {
        SubOpCode = subOpCode;

        StateId = stateId;
        GroupId = groupId;
        GameId = gameId;
    }

    public virtual void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(StateId);
        writer.Write(GroupId);
        writer.Write(GameId);
    }
}
