using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Targets;

public sealed class TargetCharacterBone : TargetCharacterGuid
{
    public string Bone = null!;

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(Bone);
    }
}