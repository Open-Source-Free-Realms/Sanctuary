using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RewardBasePacket
{
    public const short OpCode = 50;

    private readonly byte _subOpCode;

    public RewardBasePacket(byte subOpCode)
    {
        _subOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(_subOpCode);
    }
}
