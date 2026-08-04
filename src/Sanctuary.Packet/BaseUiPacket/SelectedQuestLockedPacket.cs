using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Client sends this whenever the lock-state of whichever quest it's currently displaying (journal
// selection / tracker) CHANGES - edge-triggered client-side (traced via the client's own dedupe
// check against a cached last-sent value), not sent on every UI refresh. That's why it's observed
// firing once per login/zone-load and then going quiet. Wire format confirmed via the client's
// packet constructor (FUN_00c79f00, single byte field at object offset +0xC) and its call site
// (FUN_00c7c5d0): payload byte is `IsLocked`, no QuestId - the server is expected to already know
// which quest is currently tracked (Player.ActiveQuestId) if it needs to correlate this. No reply
// is expected by the client anywhere in the traced send path.
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
