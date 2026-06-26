using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class FixtureInstance : ISerializableType
{
    public ulong Guid;
    public ulong HouseGuid;

    public int FixtureDefinitionId;

    public int Unknown4;

    public Vector4 Position;
    public Quaternion Rotation;

    public string? Unknown12;

    public float Scale;

    public int TintId;
    public int Unknown10;

    public CustomizationDetail Customization = new();

    public Quaternion Tilt;

    public ulong NpcGuid;

    public string? Unknown11;

    public int Unknown13;

    public string? XmlData;

    public bool Unknown16;

    public int Unknown17;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);
        writer.Write(HouseGuid);

        writer.Write(FixtureDefinitionId);

        writer.Write(Unknown4);

        writer.Write(Position);
        writer.Write(Rotation);

        writer.Write(Tilt);

        writer.Write(NpcGuid);

        writer.Write(TintId);

        writer.Write(Unknown10);

        Customization.Serialize(writer);

        writer.Write(Unknown11);
        writer.Write(Unknown12);
        writer.Write(Unknown13);

        writer.Write(XmlData);

        writer.Write(Scale);

        writer.Write(Unknown16);

        writer.Write(Unknown17);
    }
}