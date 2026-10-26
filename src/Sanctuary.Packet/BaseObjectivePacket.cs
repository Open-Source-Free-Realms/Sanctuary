using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseObjectivePacket
{
    public const short OpCode = 45;

    private byte SubOpCode;

    public BaseObjectivePacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) || subOpCode != SubOpCode)
            return false;

        return true;
    }
}
