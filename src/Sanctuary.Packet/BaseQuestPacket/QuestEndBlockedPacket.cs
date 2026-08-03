using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client plain message popup (case 16: FUN_00c7cd40 -> FUN_00c7b3b0 -> FUN_00c7ad30).
// The client resolves TextId as a Global.Text (T4) id and shows it in a standalone
// MessageWindow (Lua "MessageWindow:SetText" then "MessageWindow:Show") - NOT the quest journal or
// the quest-complete end screen. Retail uses this for "quest end blocked" notices. Not currently sent
// anywhere - the mid-quest reply bubble ended up using CommandPacketShowDialog instead - but the wire
// format below is real, so it's here if a plain popup is ever needed.
// After the 6-byte header the deserializer reads, in order:
//   8 bytes -> obj+0x10/+0x14  (NPC guid, read as one pair - the speaker)
//   int     -> obj+0x18        (TextId - resolved via Global.Text and shown in the window)
//   int     -> obj+0x1c        (QuestId)
// Total 22 bytes = 6 header + 16 payload. The deserializer requires the buffer exactly consumed.
public class QuestEndBlockedPacket : BaseQuestPacket, ISerializablePacket
{
    public const int SubOpCode = 16;

    public ulong NpcGuid;   // obj+0x10/+0x14 - the speaking NPC
    public int TextId;      // obj+0x18 - Global.Text id shown in the MessageWindow
    public int QuestId;     // obj+0x1c

    public QuestEndBlockedPacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(16) = 6-byte header

        writer.Write(NpcGuid);
        writer.Write(TextId);
        writer.Write(QuestId);

        return writer.Buffer;
    }
}
