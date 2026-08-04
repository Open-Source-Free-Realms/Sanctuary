using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client. Sets the "current objective target" driving the tracker arrow, mini-map indicator, and "Take Me There" breadcrumb trail. Wire format from client deserializer FUN_00a8b440 (opcode 47, sub 14); Active=false ends the packet early and clears the client's target.
public class ObjectiveTargetUpdatePacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 14;

    public bool Active = true;

    public float LocationX;
    public float LocationZ;
    public int ZoneId;
    public ulong Guid;
    public int NameId;
    public float PositionX;
    public float PositionY;
    public float PositionZ;
    public float PositionW = 1f;
    public int ScreenAngle;

    public ObjectiveTargetUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Active);

        if (Active)
        {
            writer.Write(LocationX);
            writer.Write(LocationZ);
            writer.Write(ZoneId);
            writer.Write(Guid);
            writer.Write(NameId);
            writer.Write(PositionX);
            writer.Write(PositionY);
            writer.Write(PositionZ);
            writer.Write(PositionW);
            writer.Write(ScreenAngle);
        }

        return writer.Buffer;
    }
}
