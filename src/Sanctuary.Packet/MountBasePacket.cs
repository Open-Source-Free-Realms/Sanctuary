using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class MountBasePacket
{
    public const short OpCode = 168;

    private byte SubOpCode;

    public MountBasePacket(byte subOpCode)
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

        if (!reader.TryRead(out byte subOpCode) && subOpCode != SubOpCode)
            return false;

        return true;
    }
}