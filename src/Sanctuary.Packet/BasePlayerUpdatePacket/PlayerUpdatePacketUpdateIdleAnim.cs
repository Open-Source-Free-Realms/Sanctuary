using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateIdleAnim : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 26;

    public ulong Guid;
    public int AnimationId;

    public PlayerUpdatePacketUpdateIdleAnim() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(AnimationId);

        return writer.Buffer;
    }
}
