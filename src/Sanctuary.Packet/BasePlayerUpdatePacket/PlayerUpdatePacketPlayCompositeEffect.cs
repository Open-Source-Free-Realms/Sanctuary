using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketPlayCompositeEffect : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 16;

    public ulong Guid;
    public ulong Unknown2;

    public int CompositeEffectId;

    public int Unknown4;

    public int EffectDelay;

    public Vector4 Position;

    public bool Unknown7;

    public PlayerUpdatePacketPlayCompositeEffect() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Guid);
        writer.Write(Unknown2);

        writer.Write(CompositeEffectId);

        writer.Write(Unknown4);

        writer.Write(EffectDelay);

        writer.Write(Position);

        writer.Write(Unknown7);

        return writer.Buffer;
    }
}