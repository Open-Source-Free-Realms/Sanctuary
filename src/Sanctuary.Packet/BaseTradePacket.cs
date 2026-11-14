using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

// Unlike BaseCommandPacket, trade uses a one-byte subtype.
public abstract class BaseTradePacket : ISerializablePacket
{
    public const short OpCode = 59;
    protected abstract byte SubOpCode { get; }
    protected abstract void WriteBody(PacketWriter writer);

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        writer.Write(OpCode);
        writer.Write(SubOpCode);
        WriteBody(writer);
        return writer.Buffer;
    }
}
