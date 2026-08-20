using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseRatingPacket
{
    public const short OpCode = 174;

    private readonly byte _subOpCode;

    protected BaseRatingPacket(byte subOpCode)
    {
        _subOpCode = subOpCode;
    }

    protected void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(_subOpCode);
    }
}
