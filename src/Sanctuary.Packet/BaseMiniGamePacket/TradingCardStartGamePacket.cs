using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class TradingCardStartGamePacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 40;

    public long LaunchTicket;

    public TradingCardStartGamePacket() : base(OpCode, 0, -1, -1)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(LaunchTicket);

        return writer.Buffer;
    }
}