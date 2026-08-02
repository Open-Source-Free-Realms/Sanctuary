using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Client sends this right after accepting a quest offer (observed firing immediately after
// QuestReplyPacket with Accepted=true), carrying the QuestId. Matches PacketReaderExtensions.cs's
// opcode 47 table, sub-opcode 12. Field layout best-effort (single int32 QuestId) based on the
// observed hex payload; not verified against a live client deserializer trace.
public class SelectQuestPacket : BaseUiPacket, IDeserializable<SelectQuestPacket>
{
    public new const byte OpCode = 12;

    public int QuestId;

    public SelectQuestPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SelectQuestPacket value)
    {
        value = new SelectQuestPacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode))
            return false;

        if (!reader.TryRead(out byte subOpCode))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        return true;
    }
}
