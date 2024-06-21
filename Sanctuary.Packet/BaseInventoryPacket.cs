using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseInventoryPacket
{
    public const short OpCode = 42;

    private short SubOpCode;

    public BaseInventoryPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) && subOpCode != SubOpCode)
            return false;

        return true;
    }
}