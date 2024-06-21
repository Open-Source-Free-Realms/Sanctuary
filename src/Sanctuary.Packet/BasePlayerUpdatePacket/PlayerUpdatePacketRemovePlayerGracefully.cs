using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketRemovePlayerGracefully : PlayerUpdatePacketRemovePlayer, ISerializablePacket
{
    public new const short OpCode = 1;

    public bool Animate;
    public int Delay;
    public int EffectDelay;
    public int CompositeEffectId;
    public int Duration;

    public PlayerUpdatePacketRemovePlayerGracefully() : base(OpCode)
    {
    }

    public new byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Animate);
        writer.Write(Delay);
        writer.Write(EffectDelay);
        writer.Write(CompositeEffectId);
        writer.Write(Duration);

        return writer.Buffer;
    }
}