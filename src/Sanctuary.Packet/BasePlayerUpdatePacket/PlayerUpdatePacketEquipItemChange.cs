using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketEquipItemChange : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 6;

    public ulong Guid;

    public int Id;

    // Not read by the client.
    public int Unknown;

    public CharacterAttachmentData Attachment = new();

    public int ProfileId;

    public int WieldType;

    public PlayerUpdatePacketEquipItemChange() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(Id);

        Attachment.Serialize(writer);

        writer.Write(ProfileId);

        writer.Write(Unknown);

        writer.Write(WieldType);

        return writer.Buffer;
    }
}