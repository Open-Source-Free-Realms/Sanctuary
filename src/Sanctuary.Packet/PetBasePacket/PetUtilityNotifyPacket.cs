using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetUtilityNotifyPacket : PetBasePacket, ISerializablePacket
{
    public new const byte OpCode = 20;

    public int PetId;

    public int NotificationType;

    public int GroomDurationSeconds;

    public int Unknown4;

    public int Unknown5;

    public int Unknown6;

    public PetUtilityNotifyPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PetId);
        writer.Write(NotificationType);
        writer.Write(GroomDurationSeconds);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);

        return writer.Buffer;
    }
}
