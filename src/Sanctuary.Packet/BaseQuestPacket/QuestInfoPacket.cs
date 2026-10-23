using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class QuestInfoPacket : BaseQuestPacket, ISerializablePacket
{
    public new const int OpCode = 1;

    public int QuestId;
    public int TitleId;
    public int DescriptionId;
    public int HelperTextId;
    public int IconId;
    public int GoalsTextId;
    public bool MembersOnly;

    public ulong NpcGuid;

    // Dialog camera distance from the NPC's head; the client clamps it to 2-20.
    public float CameraDistance;

    // Never read by the client.
    public bool Unknown11;

    // Hides the Decline button.
    public bool AcceptOnly;

    public RewardBundleBase RewardBundle { get; } = new();

    public QuestInfoPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(TitleId);
        writer.Write(DescriptionId);
        writer.Write(HelperTextId);
        writer.Write(IconId);
        writer.Write(GoalsTextId);
        writer.Write(MembersOnly);

        RewardBundle.Serialize(writer);

        writer.Write(NpcGuid);
        writer.Write(CameraDistance);
        writer.Write(Unknown11);
        writer.Write(AcceptOnly);

        return writer.Buffer;
    }
}
