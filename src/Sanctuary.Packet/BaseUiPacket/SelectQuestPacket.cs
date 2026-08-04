using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Client sends this right after accepting a quest offer, carrying the QuestId. Opcode 47 sub 12; field layout (single int32 QuestId) is best-effort from the observed hex payload, not a verified deserializer trace.
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
