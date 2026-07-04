using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class UnderwaterFishSpawnInfo : ISerializableType
{
    public int Unknown;

    public int ModelId;

    public string? TintAlias;
    public string? TextureAlias;

    public int Unknown5;  // size class 1..4
    public int Unknown6;

    public bool Unknown7; // catchable flag

    // Unknown8..17 are read by the client as FLOATS (fish swim/turn speeds and wander timers).
    // Sending int 1 here = 1.4e-45 (denormal ~0) which FREEZES the fish. See FISHING_RE_NOTES.md.
    public float Unknown8;   // approach time (>0.25)
    public float Unknown9;   // reel-in speed divisor
    public float Unknown10;  // nibble/flee swim speed
    public float Unknown11;  // (unused by tick)
    public float Unknown12;  // reel-in base speed offset
    public float Unknown13;  // turn/rotation speed
    public float Unknown14;  // wander speed
    public float Unknown15;  // wander deceleration
    public float Unknown16;  // approach/wander turn rate
    public float Unknown17;  // wander idle-time min

    public float Unknown18;  // wander idle-time max

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);

        writer.Write(ModelId);

        writer.Write(TintAlias);
        writer.Write(TextureAlias);

        writer.Write(Unknown5);
        writer.Write(Unknown6);

        writer.Write(Unknown7);

        writer.Write(Unknown8);
        writer.Write(Unknown9);
        writer.Write(Unknown10);
        writer.Write(Unknown11);
        writer.Write(Unknown12);
        writer.Write(Unknown13);
        writer.Write(Unknown14);
        writer.Write(Unknown15);
        writer.Write(Unknown16);
        writer.Write(Unknown17);

        writer.Write(Unknown18);
    }
}