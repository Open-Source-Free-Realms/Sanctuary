using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ActivityManagerBasePacket
{
    public const short OpCode = 147;

    private byte SubOpCode;

    public ActivityManagerBasePacket(byte subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}