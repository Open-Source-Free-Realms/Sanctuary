using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client quest turn-in / completion screen (case 13: FUN_00c7cd40 -> FUN_00c7bbd0 ->
// FUN_00c7b990). Drives the client's "Quest Complete" end screen (QuestHandler:ShowEndScreen) and
// the completion camera close-up on the turn-in NPC (processor FUN_00a95420 focuses the "HEAD"
// bone, same path the offer uses). After the 6-byte header the deserializer reads, in order:
//   8 bytes  -> obj+0x10/+0x14  (NPC guid - the camera focus target)
//   int      -> obj+0x18        (QuestId - the client echoes THIS back in QuestEndReplyPacket, so
//                                it must match the accepted quest's id or the objective/journal
//                                never clears)
//   int      -> obj+0x1c        (title text id)
//   int      -> obj+0x20        (description text id)
//   RewardBundleBase (FUN_008e7930, 69 bytes)
//   float    -> obj+0xe0        (completion %, NaN-checked)
// Total 99 bytes = 6 header + 93 payload.
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
