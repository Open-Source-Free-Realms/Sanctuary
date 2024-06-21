using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketDismountResponse : MountBasePacket, ISerializablePacket
{
    public new const byte OpCode = 4;

    public ulong RiderGuid;

    public int CompositeEffectId;

    public PacketDismountResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(RiderGuid);
        writer.Write(CompositeEffectId);

        return writer.Buffer;
    }
}