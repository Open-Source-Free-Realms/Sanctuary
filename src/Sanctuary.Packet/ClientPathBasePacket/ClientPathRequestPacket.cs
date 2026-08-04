using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Client -> server (opcode 98, sub 1). A path request for the "Take Me There" breadcrumb system.
// Payload (52 bytes): int RequestId, int, int Mode, int, Vector4 Start, Vector4 End, int.
// Mode distinguishes the two situations the client sends this in (confirmed from live logs):
//   1 = a passive breadcrumb refresh - fired automatically on quest accept, on teleport, and as the
//       player moves, only to keep the green trail pointing at the objective (must NOT auto-walk).
//   2 = the actual "Take Me There" button click (each is followed by the QuestHelper:takeMeThere UI
//       event) - this is the one that should auto-walk the character.
// The server replies with ClientPathReplyPacket carrying the path from Start to the destination.
public class ClientPathRequestPacket : ClientPathBasePacket, IDeserializable<ClientPathRequestPacket>
{
    public new const byte OpCode = 1;

    public int RequestId;
    public int Unknown1;
    public int Mode;      // 1 = passive trail refresh, 2 = "Take Me There" button click (auto-walk)
    public int Unknown3;
    public Vector4 Start;
    public Vector4 End;
    public int Unknown4;

    public ClientPathRequestPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientPathRequestPacket value)
    {
        value = new ClientPathRequestPacket();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode)) return false;
        if (!reader.TryRead(out byte subOpCode)) return false;

        if (!reader.TryRead(out value.RequestId)) return false;
        if (!reader.TryRead(out value.Unknown1)) return false;
        if (!reader.TryRead(out value.Mode)) return false;
        if (!reader.TryRead(out value.Unknown3)) return false;
        if (!reader.TryRead(out value.Start)) return false;
        if (!reader.TryRead(out value.End)) return false;
        if (!reader.TryRead(out value.Unknown4)) return false;

        return true;
    }
}
