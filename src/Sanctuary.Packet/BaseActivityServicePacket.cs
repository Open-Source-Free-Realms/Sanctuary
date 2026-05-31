using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseActivityServicePacket
{
    public const short OpCode = 167;

    private byte SubOpCode;

    public BaseActivityServicePacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public virtual void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }

    public virtual bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) && subOpCode != SubOpCode)
            return false;

        return true;
    }
}