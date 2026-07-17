using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;


public class PlayerUpdatePacketKnockback : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 4;

    public ulong Guid;

    public int Unknown;

    public Vector4 Position;

    /// <summary>Unit XZ direction vector.</summary>
    public Vector4 Direction;

    public float Magnitude;

    public PlayerUpdatePacketKnockback() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Unknown);
        writer.Write(Position);
        writer.Write(Direction);
        writer.Write(Magnitude);

        return writer.Buffer;
    }
}
