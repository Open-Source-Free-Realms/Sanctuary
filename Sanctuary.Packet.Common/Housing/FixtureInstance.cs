using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class FixtureInstance : ISerializableType
{
    public ulong Guid;
    public ulong HouseGuid;

    public int Id;
    public int Unknown4;

    public Vector4 Unknown5;
    public Quaternion Unknown6;

    public string? Unknown12;

    public float Unknown15;

    public int Unknown9;
    public int Unknown10;

    public CustomizationDetail CustomizationDetails = new();

    public Quaternion Unknown7;

    public long Unknown8;

    public string? Unknown11;

    public int Unknown13;

    public string? Unknown14;

    public bool Unknown16;

    public int Unknown17;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);
        writer.Write(HouseGuid);

        writer.Write(Id);
        writer.Write(Unknown4);

        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);

        writer.Write(Unknown8);

        writer.Write(Unknown9);
        writer.Write(Unknown10);

        CustomizationDetails.Serialize(writer);

        writer.Write(Unknown11);
        writer.Write(Unknown12);
        writer.Write(Unknown13);
        writer.Write(Unknown14);
        writer.Write(Unknown15);

        writer.Write(Unknown16);

        writer.Write(Unknown17);
    }
}