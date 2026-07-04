using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class FishingPlayerConfig : ISerializableType
{
    public int Unknown;

    public int Unknown16;
    public int Unknown14;
    public int Unknown15;

    public int Unknown7;

    // Client reads these two as floats: min/max cast distance. The aiming state only validates a
    // cast when the water-raycast hit distance is within [Unknown2, Unknown3]; leaving them 0
    // makes casting impossible. (Server config defaults: MinCastDistance 3.0, MaxCastDistance 20.0.)
    public float Unknown2;
    public float Unknown3;

    public int Unknown4;
    public int Unknown5;
    public int Unknown6;
    public int Unknown8;
    public int Unknown9;

    public float Unknown10; // 6.0f
    public float Unknown11; // 0.444f
    public float Unknown12; // 1.85f
    public float Unknown13; // 0.2f

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
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
    }
}