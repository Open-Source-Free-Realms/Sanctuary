using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateWalkAnim : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 21;

    public ulong Guid;

    public int Animation;

    public PlayerUpdatePacketUpdateWalkAnim() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Animation);

        return writer.Buffer;
    }
}
