using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Client -> server: reply to CommandPacketShowDialog when the player clicks one of its response
// buttons. Opcode 26 sub 6 (BaseCommandPacket header). Confirmed live against the client: clicking
// "You got it!" (the mid-quest reply bubble's Response.Id = 1, see CommandPacketShowDialog.cs) sends
// exactly short OpCode(26) + short SubOpCode(6) + int ResponseId, 8 bytes, buffer exactly consumed.
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
