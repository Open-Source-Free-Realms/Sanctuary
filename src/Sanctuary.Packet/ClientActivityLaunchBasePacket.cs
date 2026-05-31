using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientActivityLaunchBasePacket
{
    public const short OpCode = 175;

    private int SubOpCode;

    private int ActivityId;
    private int Unknown;

    public ClientActivityLaunchBasePacket(int subOpCode, int activityId, int unknown)
    {
        SubOpCode = subOpCode;

        ActivityId = activityId;
        Unknown = unknown;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(ActivityId);
        writer.Write(Unknown);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out int subOpCode) && subOpCode != SubOpCode)
            return false;

        if (!reader.TryRead(out ActivityId))
            return false;

        if (!reader.TryRead(out Unknown))
            return false;

        return true;
    }
}