using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketThoughtBubble : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 27;

    public ulong Guid;

    public int IconId;

    public int DurationSeconds;

    public bool Clear;

    public PlayerUpdatePacketThoughtBubble() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(IconId);
        writer.Write(DurationSeconds);
        writer.Write(Clear);

        return writer.Buffer;
    }
}
