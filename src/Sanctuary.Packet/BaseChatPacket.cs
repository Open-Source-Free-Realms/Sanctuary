using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseChatPacket
{
    public const short OpCode = 15;

    private short SubOpCode;

    public BaseChatPacket(short subOpCode)
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