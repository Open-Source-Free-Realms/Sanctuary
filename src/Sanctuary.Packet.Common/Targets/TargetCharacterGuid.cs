using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Targets;

public class TargetCharacterGuid : TargetBase
{
    public long Guid;

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(Guid);
    }
}