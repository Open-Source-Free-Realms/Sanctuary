using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketExpectedSpeed : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 23;

    public ulong Guid;

    public float ExpectedSpeed;

    public PlayerUpdatePacketExpectedSpeed() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(ExpectedSpeed);

        return writer.Buffer;
    }
}