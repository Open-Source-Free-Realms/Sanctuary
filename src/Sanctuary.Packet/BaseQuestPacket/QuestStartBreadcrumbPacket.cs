using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuestStartBreadcrumbPacket : BaseQuestPacket, IDeserializable<QuestStartBreadcrumbPacket>
{
    public new const int OpCode = 15;

    public int QuestId;

    public QuestStartBreadcrumbPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out QuestStartBreadcrumbPacket value)
    {
        value = new QuestStartBreadcrumbPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        return reader.RemainingLength == 0;
    }
}
