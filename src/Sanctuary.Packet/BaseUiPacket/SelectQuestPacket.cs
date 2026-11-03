using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

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

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        return reader.RemainingLength == 0;
    }
}
