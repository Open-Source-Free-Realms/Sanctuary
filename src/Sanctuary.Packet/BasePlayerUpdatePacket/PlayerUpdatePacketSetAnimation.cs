using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketSetAnimation : : BasePlayerUpdatePacket, ISerializablePacket
{
    public const short OpCode = 8;

    public ulong Guid;
    public int AnimationId;
    public int Unknown;
    public byte PlayType = 2;

    public PlayerUpdatePacketSetAnimation() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(AnimationId);
        writer.Write(Unknown);
        writer.Write(PlayType);

        return writer.Buffer;
    }
}
