using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class WallOfDataBasePacket
{
    public const short OpCode = 194;

    private byte SubOpCode;

    public WallOfDataBasePacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) && subOpCode != SubOpCode)
            return false;

        return true;
    }
}