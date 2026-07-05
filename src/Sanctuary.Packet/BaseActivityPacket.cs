using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseActivityPacket : BaseActivityServicePacket
{
    private readonly byte _activitySubOpCode;

    public BaseActivityPacket(byte activitySubOpCode) : base(1)
    {
        _activitySubOpCode = activitySubOpCode;
    }

    public override void Write(PacketWriter writer)
    {
        base.Write(writer);
        writer.Write(_activitySubOpCode);
    }
}
