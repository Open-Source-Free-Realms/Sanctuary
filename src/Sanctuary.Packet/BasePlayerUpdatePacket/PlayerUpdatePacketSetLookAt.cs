using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketSetLookAt : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 17;

    public ulong Guid;

    public ulong TargetGuid;

    public PlayerUpdatePacketSetLookAt() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(TargetGuid);

        return writer.Buffer;
    }
}
