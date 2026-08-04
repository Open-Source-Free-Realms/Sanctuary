using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client plain message popup (Lua MessageWindow). Unused - CommandPacketShowDialog is used instead.
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
