using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ObjectiveTargetUpdatePacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 14;

    public bool Active = true;

    public ulong TaskId;
    public int Unknown = 2;
    public ulong Guid;
    public int NameId;
    public float PositionX;
    public float PositionY;
    public float PositionZ;
    public float PositionW = 1f;
    public int Unknown2 = 1;

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
            writer.Write(TaskId);
            writer.Write(Unknown);
            writer.Write(Guid);
            writer.Write(NameId);
            writer.Write(PositionX);
            writer.Write(PositionY);
            writer.Write(PositionZ);
            writer.Write(PositionW);
            writer.Write(Unknown2);
        }

        return writer.Buffer;
    }
}
