using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BasePlayerTitlePacket
{
    public const short OpCode = 152;

    private int SubOpCode;

    public BasePlayerTitlePacket(int subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public virtual void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out int subOpCode) && subOpCode != SubOpCode)
            return false;

        return true;
    }
}