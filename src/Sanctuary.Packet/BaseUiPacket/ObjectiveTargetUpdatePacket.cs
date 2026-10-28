using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ObjectiveTargetUpdatePacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 14;

    public bool Active = true;

    public ulong TaskId;
    public int SelectTaskId = 2;
    public ulong Guid;
    public int NameId;
    public float PositionX;
    public float PositionY;
    public float PositionZ;
    public float PositionW = 1f;
    public int TargetType = 2;

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
            writer.Write(SelectTaskId);
            writer.Write(Guid);
            writer.Write(NameId);
            writer.Write(PositionX);
            writer.Write(PositionY);
            writer.Write(PositionZ);
            writer.Write(PositionW);
            writer.Write(TargetType);
        }

        return writer.Buffer;
    }
}
