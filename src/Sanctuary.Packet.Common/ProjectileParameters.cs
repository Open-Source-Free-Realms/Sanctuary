using System.Numerics;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common.Targets;

namespace Sanctuary.Packet.Common;

public class ProjectileParameters : ISerializableType
{
    public int Unknown;
    public int Unknown2;
    public int Unknown3;
    public int Unknown4;

    public float Unknown15;
    public float Unknown16;

    public int Unknown5;
    public int Unknown6;

    public Target Target10 = Target.CreateTarget();
    public Target Target11 = Target.CreateTarget();

    public bool Unknown14;

    public Vector4 Unknown7;
    public Vector4 Unknown8;
    public Vector4 Unknown12;

    public int Unknown13;

    public string Unknown9 = string.Empty;
    public string Unknown28 = string.Empty;

    public int Unknown17;
    public int Unknown18;
    public int Unknown19;
    public int Unknown20;
    public int Unknown21;
    public int Unknown22;
    public int Unknown23;
    public float Unknown24;

    public float Unknown25;
    public float Unknown26;
    public float Unknown27;

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
        Target10.Serialize(writer);
        Target11.Serialize(writer);
        writer.Write(Unknown12);
        writer.Write(Unknown13);
        writer.Write(Unknown14);
        writer.Write(Unknown15);
        writer.Write(Unknown16);
        writer.Write(Unknown17);
        writer.Write(Unknown18);
        writer.Write(Unknown19);
        writer.Write(Unknown20);
        writer.Write(Unknown21);
        writer.Write(Unknown22);
        writer.Write(Unknown23);
        writer.Write(Unknown24);
        writer.Write(Unknown25);
        writer.Write(Unknown26);
        writer.Write(Unknown27);
        writer.Write(Unknown28);
    }
}
