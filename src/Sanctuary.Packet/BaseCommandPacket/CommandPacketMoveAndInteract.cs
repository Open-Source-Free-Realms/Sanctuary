using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketMoveAndInteract : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 22;

    public Vector4 Position;
    public ulong Guid;

    public CommandPacketMoveAndInteract() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Position);
        writer.Write(Guid);

        return writer.Buffer;
    }
}
