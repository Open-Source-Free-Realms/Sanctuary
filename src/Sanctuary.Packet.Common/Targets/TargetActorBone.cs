using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.Targets;

public sealed class TargetActorBone : TargetBase
{
    public int ActorId;

    public string Bone = null!;

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(ActorId);

        writer.Write(Bone);
    }
}