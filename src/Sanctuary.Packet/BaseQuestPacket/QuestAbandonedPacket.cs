using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

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

        Write(writer);

        writer.Write(QuestId);
        writer.Write(false);

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

        return reader.RemainingLength == 0;
    }
}
