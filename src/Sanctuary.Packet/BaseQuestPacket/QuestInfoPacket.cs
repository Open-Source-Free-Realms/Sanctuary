using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Field layout verified LIVE via debugger (IDA local Windows debugger, breakpoint on entry,
// single-stepped through the client's actual deserializer): ClientPcData::sub_A107F0 case 1
// -> sub_C7BB60 -> sub_C7B7A0, and RewardBundleBase::sub_8E7930 / FUN_008c9d20,
// FreeRealms_2014-03-13.exe. This supersedes an earlier layout traced against sub_C7B990,
// which turned out to not be the function actually invoked at runtime - that mismatch is why
// a "byte-perfect" (against the wrong function) packet never produced any client reaction.
// Six int32 fields, a bool, a nested RewardBundleBase (18 fixed-size fields, no lists), then
// two more int32 fields, one more int32, and two more bools. 108 bytes of payload.
// Field semantics beyond QuestId are positionally confirmed but not semantically named yet.
public class QuestInfoPacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 1;

    public int QuestId;
    public int TitleId;
    public int DescriptionId;
    public int HelperTextId;
    public int IconId;
    public int Unknown6;
    public bool Unknown7;

    // Read as one 8-byte value in a single bounds check right after RewardBundleBase - the same
    // pattern used for 64-bit guids elsewhere in this protocol. Almost certainly the quest
    // giver NPC's guid, used by the client to know whose portrait/model to show in the offer
    // popup (previously sent as 0, which made the client fall back to showing the player).
    public ulong NpcGuid;

    public int Unknown10;
    public bool Unknown11;
    public bool Unknown12;

    // RewardBundleBase +0x50 - coins shown in the offer's reward preview.
    public int RewardCoins;
    // RewardBundleBase +0x48 - job/profile experience (XP) shown in the offer's reward preview.
    public int RewardExperience;
    // Item rewards shown as icons in the offer's "Show Details" reward preview.
    public List<RewardBundleItem> RewardItems = new();

    public QuestInfoPacket() : base(SubOpCode)
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
        writer.Write(Unknown6);
        writer.Write(Unknown7);

        // RewardBundleBase - coins/XP + item-reward entries (icons in the offer preview).
        RewardBundleSerializer.Write(writer, RewardCoins, RewardExperience, RewardItems);

        writer.Write(NpcGuid);
        writer.Write(Unknown10);
        writer.Write(Unknown11);
        writer.Write(Unknown12);

        return writer.Buffer;
    }
}
