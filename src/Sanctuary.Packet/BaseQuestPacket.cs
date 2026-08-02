using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseQuestPacket
{
    public const short OpCode = 49;

    private readonly int SubOpCode;

    public BaseQuestPacket(int subOpCode)
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
