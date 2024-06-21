using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseCoinStorePacket
{
    public const short OpCode = 165;

    private short SubOpCode;

    public BaseCoinStorePacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }
}