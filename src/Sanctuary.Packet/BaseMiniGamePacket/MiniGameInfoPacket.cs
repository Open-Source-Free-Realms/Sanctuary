using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameInfoPacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 16;

    public MiniGameInfo Info = new();

    public int Unknown2;

    public MiniGameInfoPacket(int stateId, int groupId, int gameId) : base(OpCode, stateId, groupId, gameId)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Info.Serialize(writer);

        writer.Write(Unknown2);

        return writer.Buffer;
    }
}