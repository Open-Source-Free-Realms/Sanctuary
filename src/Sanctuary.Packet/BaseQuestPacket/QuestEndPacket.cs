using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client turn-in screen; drives QuestHandler:ShowEndScreen and the NPC camera close-up.
public class QuestEndPacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 13;

    public ulong NpcGuid;        // obj+0x10/+0x14 - turn-in NPC, drives the camera close-up
    public int QuestId;          // obj+0x18 - echoed in QuestEndReply; must match the active quest
    public int TitleId;          // obj+0x1c
    public int DescriptionId;    // obj+0x20
    public float Percent = 1f;   // obj+0xe0

    public int RewardCoins;      // RewardBundleBase +0x50
    public int RewardExperience; // RewardBundleBase +0x48 - job/profile experience (XP)
    // Item rewards shown as icons in the turn-in "Show Details" reward preview.
    public List<RewardBundleItem> RewardItems = new();

    public QuestEndPacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(13) = 6-byte header

        writer.Write(NpcGuid);
        writer.Write(QuestId);
        writer.Write(TitleId);
        writer.Write(DescriptionId);

        // RewardBundleBase - coins/XP + item-reward entries (icons in the turn-in preview).
        RewardBundleSerializer.Write(writer, RewardCoins, RewardExperience, RewardItems);

        writer.Write(Percent);

        return writer.Buffer;
    }
}
