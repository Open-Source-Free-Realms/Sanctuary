using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateActiveWieldType : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 61;

    public ulong Guid;

    public int WieldType;

    public PlayerUpdatePacketUpdateActiveWieldType() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(WieldType);

        return writer.Buffer;
    }
}
