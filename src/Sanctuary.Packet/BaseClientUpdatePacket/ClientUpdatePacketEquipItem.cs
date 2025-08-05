using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ClientUpdatePacketEquipItem : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public int Guid;

    public CharacterAttachmentData Attachment = new();

    public int ProfileId;

    private int Unused = default;

    public bool Equip = true;

    public ClientUpdatePacketEquipItem() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        Attachment.Serialize(writer);

        writer.Write(ProfileId);

        writer.Write(Unused);

        writer.Write(Equip);

        return writer.Buffer;
    }
}