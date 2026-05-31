using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MiniGameLeavePacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 19;

    public MiniGameLeavePacket(int stateId) : base(OpCode, stateId, -1, -1)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }
}