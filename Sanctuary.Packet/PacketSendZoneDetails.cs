using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketSendZoneDetails : ISerializablePacket
{
    public const short OpCode = 43;

    public string Name = null!;

    // 0 - TileStatic
    // 1 - TileSeamless
    // 2 - RuntimeSeamless
    // 3 - Mesh

    private int Type = 2; // Should always be 2.

    public bool Tutorial;
    public bool Unknown2;

    public string? Sky;

    public bool InCombatZone;

    public int Id;

    public int GeometryId;

    public bool IsInStartingSocialZone;
    public bool Unknown6;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Name);
        writer.Write(Type);

        writer.Write(Tutorial);
        writer.Write(Unknown2);

        writer.Write(Sky);

        writer.Write(InCombatZone);

        writer.Write(Id);

        writer.Write(GeometryId);
        writer.Write(IsInStartingSocialZone);
        writer.Write(Unknown6);

        return writer.Buffer;
    }
}