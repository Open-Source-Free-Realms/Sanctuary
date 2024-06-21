using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BasePlayerUpdatePacket
{
    public const short OpCode = 35;

    private short SubOpCode;

    public BasePlayerUpdatePacket(short subOpCode)
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

        if (!reader.TryRead(out short subOpCode) && subOpCode != SubOpCode)
            return false;

        return true;
    }
}