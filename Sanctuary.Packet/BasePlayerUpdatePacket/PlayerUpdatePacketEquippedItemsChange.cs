using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketEquippedItemsChange : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 7;

    public ulong Guid;

    public List<CharacterAttachmentData> Attachments = new();

    public int Unknown;

    public PlayerUpdatePacketEquippedItemsChange() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(Attachments);

        writer.Write(Unknown);

        return writer.Buffer;
    }
}