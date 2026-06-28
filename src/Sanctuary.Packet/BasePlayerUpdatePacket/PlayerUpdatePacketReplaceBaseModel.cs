using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketReplaceBaseModel : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 49;

    public ulong Guid;
    public int ModelId;

    public PlayerUpdatePacketReplaceBaseModel() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(ModelId);

        return writer.Buffer;
    }
}
