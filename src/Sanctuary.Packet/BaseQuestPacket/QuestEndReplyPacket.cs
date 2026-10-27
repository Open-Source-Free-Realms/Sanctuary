using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuestEndReplyPacket : BaseQuestPacket, IDeserializable<QuestEndReplyPacket>
{
    public new const int OpCode = 14;

    public int QuestId;

    public QuestEndReplyPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out QuestEndReplyPacket value)
    {
        value = new QuestEndReplyPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        return reader.RemainingLength == 0;
    }
}
