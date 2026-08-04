using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client: removes a quest from the Hero's Journal. Trailing bool must be false, or the
// dispatcher's extra journal/UI refresh (FUN_00a92cd0) is skipped.
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
