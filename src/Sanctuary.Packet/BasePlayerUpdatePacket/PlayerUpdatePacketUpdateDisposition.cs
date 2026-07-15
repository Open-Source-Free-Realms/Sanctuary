using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op35 sub28 — per-NPC disposition override. The client's nameplate color resolver runs when
// NameColor == 0 and colors the overhead name by disposition: 0 hostile = red, 1 neutral / 2 ally =
// the bluish default. AddNpc's own Disposition field is ignored for this (the client derives it from
// the encounter arena flag, defaulting to ally) — so send Disposition=0 after AddNpc to get a
// red-named hostile.
public class PlayerUpdatePacketUpdateDisposition : ISerializablePacket
{
    public const short OpCode = 35;
    public const short SubOpCode = 28;

    public ulong Guid;
    public int Disposition;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(Guid);
        writer.Write(Disposition);

        return writer.Buffer;
    }
}
