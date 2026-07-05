using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseActivityServicePacket
{
    public const short OpCode = 167;

    private readonly byte _serviceSubOpCode;

    public BaseActivityServicePacket(byte serviceSubOpCode)
    {
        _serviceSubOpCode = serviceSubOpCode;
    }

    public virtual void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(_serviceSubOpCode);
    }
}
