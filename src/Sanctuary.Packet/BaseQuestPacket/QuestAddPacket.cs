using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Objectives list is written empty here; objectives are added separately via QuestObjectiveAddedPacket.
public class QuestAddPacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 3;

    public int QuestId;
    public int TitleId;
    public int DescriptionId;
    public int HelperTextId;
    public bool MembersOnly;
    public long TimeStarted;
    public int ProfileId;
    public float CompletedPercentage;
    public int IconId;
    public bool SystemQuest;

    // Optional inline objective (sub_92B050 list); when set, the objective is loaded into
    // ClientQuestData up front so it can be tracked and its breadcrumb activated immediately.
    public bool IncludeObjective;
    public int ObjectiveId;            // per-entry leading int (sub_92B050 local_4)
    public int ObjectiveNameId;        // body int0
    public int ObjectiveDescriptionId; // body int1
    public int ObjectiveField2;        // body int2

    public QuestAddPacket() : base(SubOpCode)
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
        writer.Write(MembersOnly);
        writer.Write(TimeStarted);
        writer.Write(ProfileId);
        writer.Write(false); // GAP_3_ClientQuestData[0] bool
        writer.Write(CompletedPercentage);

        // RewardBundleBase (RewardBundleBase::sub_8E7930's read order) - empty; the real reward bundle
        // is sent separately via QuestInfoPacket (offer) / QuestEndPacket (turn-in).
        RewardBundleSerializer.Write(writer, 0, 0);

        // Objectives list (sub_92B050): count, then per entry [int leadingId + sub_8FD770 103-byte body].
        if (IncludeObjective)
        {
            writer.Write(1); // count

            writer.Write(ObjectiveId); // per-entry leading int (local_4)

            // sub_8FD770 objective body (103 bytes) - identical layout to QuestObjectiveAddedPacket.
            writer.Write(ObjectiveNameId);        // int0
            writer.Write(ObjectiveDescriptionId); // int1
            writer.Write(ObjectiveField2);        // int2
            writer.Write(false);                  // bool

            // RewardBundleBase (sub_8E7930) - empty, same as above.
            RewardBundleSerializer.Write(writer, 0, 0);

            // trailing objective fields: int, int, int, int, bool, int
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(false); writer.Write(0);
        }
        else
        {
            writer.Write(0); // empty list
        }

        writer.Write(IconId);
        writer.Write(SystemQuest);
        writer.Write(false); // ClientQuestData::m_bUnknown
        writer.Write(false); // trailing bool read directly in sub_C7CC80

        return writer.Buffer;
    }
}
