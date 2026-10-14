using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetTrickNotifyPacket : PetBasePacket, ISerializablePacket
{
    public new const byte OpCode = 15;

    public int PetId;

    public int NotificationType;

    public int TrickId;

    public int NameId;

    public int Unknown5;

    public PetTrickNotifyPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PetId);
        writer.Write(NotificationType);
        writer.Write(TrickId);
        writer.Write(NameId);
        writer.Write(Unknown5);

        return writer.Buffer;
    }
}
