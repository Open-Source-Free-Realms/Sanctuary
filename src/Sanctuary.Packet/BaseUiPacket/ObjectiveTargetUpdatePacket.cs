using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Server -> client. Sets the "current objective target" the client uses to draw the tracker arrow,
// the mini-map indicator, and - crucially - the "Take Me There" button + green breadcrumb trail on the
// floor. The client keeps a single active target (ObjectiveTargetDataSource); sending this replaces it.
// Wire format reverse-engineered from the client deserializer FUN_00a8b440 (opcode 47, sub-opcode 14):
//   bool  Active           - if false the packet ends here and the client clears its target
//   float LocationX        - 2D map X   (client field +0x10)
//   float LocationZ        - 2D map Z   (client field +0x14)
//   int   ZoneId           - area id of the target (+0x18); a change in these three fires the recompute
//   ulong Guid             - target entity guid (+0x20)
//   int   NameId           - (+0x28) display-name id; the setter FUN_00cb85b0 resolves it to the label
//                            shown on the tracker/mini-map. 0/invalid -> "Default Housing NPC" fallback.
//   float PositionX/Y/Z/W  - full 3D target position, read as a Vector4 with NaN guards (+0x30)
//   int   ScreenAngle      - (+0x40) trailing field; 0 is accepted
// The deserializer requires the buffer to be fully consumed, so the field count/size must match exactly.
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
