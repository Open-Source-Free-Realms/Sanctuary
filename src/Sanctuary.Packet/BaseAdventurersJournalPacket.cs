using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseAdventurersJournalPacket
{
    public const short OpCode = 209;

    private int SubOpCode;

    public BaseAdventurersJournalPacket(int subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}