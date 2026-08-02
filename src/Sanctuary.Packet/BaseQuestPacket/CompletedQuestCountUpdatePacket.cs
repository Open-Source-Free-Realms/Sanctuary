using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: updates the player's lifetime "quests completed" counter (op49 sub 12), shown in
// the Hero's Journal / player stats UI. Body is a single int32 after the 6-byte op49 header (same
// minimal shape as QuestCompletePacket; needs a live once-over when first observed in-game — if the
// counter doesn't move, the client wants additional fields).
public class CompletedQuestCountUpdatePacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 12;

    // Total number of quests this character has ever completed.
    public int Count;

    public CompletedQuestCountUpdatePacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(12)

        writer.Write(Count);

        return writer.Buffer;
    }
}
