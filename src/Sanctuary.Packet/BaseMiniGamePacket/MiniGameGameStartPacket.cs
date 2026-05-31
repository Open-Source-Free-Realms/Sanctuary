using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameGameStartPacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 17;

    public MiniGameGameStartPacket(int stateId, int groupId, int gameId) : base(OpCode, stateId, groupId, gameId)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }
}