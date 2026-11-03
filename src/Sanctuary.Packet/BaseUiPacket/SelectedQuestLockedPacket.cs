using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class SelectedQuestLockedPacket : BaseUiPacket, IDeserializable<SelectedQuestLockedPacket>
{
    public new const byte OpCode = 13;

    public bool IsLocked;

    public SelectedQuestLockedPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out SelectedQuestLockedPacket value)
    {
        value = new SelectedQuestLockedPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.IsLocked))
            return false;

        return reader.RemainingLength == 0;
    }
}
