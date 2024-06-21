using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseUiPacket
{
    public const short OpCode = 47;

    private byte SubOpCode;

    public BaseUiPacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}