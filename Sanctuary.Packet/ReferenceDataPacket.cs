using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ReferenceDataPacket
{
    public const short OpCode = 44;

    private short SubOpCode;

    public ReferenceDataPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}