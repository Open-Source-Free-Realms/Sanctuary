using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketSetAnimation : ISerializablePacket
{
    public const short OpCode = 35;
    public const short SubOpCode = 8;

    public ulong Guid;
    public int AnimationId;
    public int Unknown;
    public byte PlayType = 2;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(Guid);
        writer.Write(AnimationId);
        writer.Write(Unknown);
        writer.Write(PlayType);

        return writer.Buffer;
    }
}
