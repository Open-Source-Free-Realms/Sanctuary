using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: tells the client a quest was abandoned so it removes the entry from the
// Hero's Journal. Sent by the server after it processes a CommandPacketQuestAbandon (opcode 26,
// sub-opcode 23 - the journal "Drop Quest" button). Sub-opcode 6 in the opcode-49 table.
// Wire format traced from the client deserializer (FUN_00c7b500 -> FUN_00c7ae40, which requires
// the buffer to be consumed EXACTLY): 6-byte header (short 49 + int 6) + int QuestId + one trailing
// bool = 11 bytes. This is one byte longer than QuestCompletePacket (sub 4), which has no trailing
// bool - sending only 10 bytes here makes the client reject the packet (buffer underrun) so the
// journal entry is never removed. The trailing bool is read into the packet object at +0x10; the
// dispatcher runs an extra journal/UI refresh (FUN_00a92cd0) only when it is 0, so we send false.
// (Also kept deserializable in case the client ever echoes it.)
public class QuestAbandonedPacket : BaseQuestPacket, IDeserializable<QuestAbandonedPacket>, ISerializablePacket
{
    public new const int OpCode = 6;

    public int QuestId;

    public QuestAbandonedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // short OpCode(49) + int SubOpCode(6) = 6-byte header

        writer.Write(QuestId);
        writer.Write(false); // trailing bool (FUN_00c7ae40 reads it) - required or the client rejects the packet

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out QuestAbandonedPacket value)
    {
        value = new QuestAbandonedPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        return true;
    }
}
