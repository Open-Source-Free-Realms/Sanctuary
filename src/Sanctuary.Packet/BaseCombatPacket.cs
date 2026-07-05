using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseCombatPacket
{
    public const short OpCode = 32;

    public short SubOpCode;

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out SubOpCode))
            return false;

        return true;
    }
}
