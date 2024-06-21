using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseEncounterPacket
{
    public const short OpCode = 41;

    private short SubOpCode;

    public int Unknown;
    public int Unknown2;

    public BaseEncounterPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}