using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Traced sub_A107F0 -> sub_C7BB60 -> sub_C7B7A0. Field semantics beyond QuestId are positional.
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

    // Read as a single 8-byte value right after RewardBundleBase; almost certainly the quest
    // giver NPC's guid, used to pick the portrait/model shown in the offer popup.
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
