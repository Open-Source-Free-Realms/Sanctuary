using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseClientUpdatePacket
{
    public const short OpCode = 38;

    private short SubOpCode;

    public BaseClientUpdatePacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}