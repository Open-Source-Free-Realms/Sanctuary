using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ObjectiveClientClearPacket : BaseObjectivePacket, ISerializablePacket
{
    public new const byte OpCode = 8;

    public int Unknown;

    public ObjectiveClientClearPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown);

        return writer.Buffer;
    }
}
