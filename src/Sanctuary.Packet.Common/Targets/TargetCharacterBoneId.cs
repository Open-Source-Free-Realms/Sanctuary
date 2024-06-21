using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Targets;

public sealed class TargetCharacterBoneId : TargetCharacterGuid
{
    public int BoneId;

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(BoneId);
    }
}