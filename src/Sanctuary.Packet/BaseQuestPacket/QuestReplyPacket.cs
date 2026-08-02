using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Best-effort field layout - not verified against a live client capture. Incoming reply to
// a QuestInfoPacket offer (accept/decline).
public class QuestReplyPacket : BaseQuestPacket, IDeserializable<QuestReplyPacket>
{
    public new const int OpCode = 2;

    public int QuestId;
    public bool Accepted;

    public QuestReplyPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out QuestReplyPacket value)
    {
        value = new QuestReplyPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        if (!reader.TryRead(out value.Accepted))
            return false;

        return true;
    }
}
