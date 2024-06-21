using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Targets;

public sealed class TargetActorBoneId : TargetBase
{
    public int ActorId;

    public string BoneId = null!;

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(ActorId);

        writer.Write(BoneId);
    }
}