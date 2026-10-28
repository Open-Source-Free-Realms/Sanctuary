using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class QuestEndPacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 13;

    public ulong NpcGuid;
    public int QuestId;
    public int EndDialogTextId;
    public int NpcAnimationId;
    public float CameraDistance;

    public RewardBundleBase RewardBundle { get; } = new();

    public QuestEndPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(NpcGuid);
        writer.Write(QuestId);
        writer.Write(EndDialogTextId);
        writer.Write(NpcAnimationId);

        RewardBundle.Serialize(writer);

        writer.Write(CameraDistance);

        return writer.Buffer;
    }
}
