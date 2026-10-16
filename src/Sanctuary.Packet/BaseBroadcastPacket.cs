using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseBroadcastPacket
{
    public const short OpCode = 77;

    private short SubOpCode;

    public BaseBroadcastPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public virtual void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}
