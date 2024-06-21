using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketMountResponse : MountBasePacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public ulong RiderGuid;
    public ulong MountGuid;

    public int Seat;

    public int QueuePosition;

    public int Unknown;

    public int CompositeEffectId;

    public float NameVerticalOffset;

    public PacketMountResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(RiderGuid);
        writer.Write(MountGuid);

        writer.Write(Seat);

        writer.Write(QueuePosition);

        writer.Write(Unknown);

        writer.Write(CompositeEffectId);

        writer.Write(NameVerticalOffset);

        return writer.Buffer;
    }
}