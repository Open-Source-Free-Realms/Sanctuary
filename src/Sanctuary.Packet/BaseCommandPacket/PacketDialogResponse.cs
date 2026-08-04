using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Client -> server: reply to CommandPacketShowDialog when the player clicks a response button. Opcode 26 sub 6, payload is a single int ResponseId (confirmed live).
public class PacketDialogResponse : BaseCommandPacket, IDeserializable<PacketDialogResponse>
{
    public new const short OpCode = 6;

    public int ResponseId;

    public PacketDialogResponse() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketDialogResponse value)
    {
        value = new PacketDialogResponse();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode))
            return false;

        if (!reader.TryRead(out short subOpCode))
            return false;

        if (!reader.TryRead(out value.ResponseId))
            return false;

        return true;
    }
}
