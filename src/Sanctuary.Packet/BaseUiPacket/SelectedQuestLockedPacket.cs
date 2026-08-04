using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Client sends this whenever the lock-state of the currently-displayed quest changes (edge-triggered, not on every UI refresh). Payload is a single `IsLocked` byte; no QuestId (FUN_00c79f00).
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

        if (!reader.TryRead(out short opCode))
            return false;

        if (!reader.TryRead(out byte subOpCode))
            return false;

        if (!reader.TryRead(out value.IsLocked))
            return false;

        return true;
    }
}
