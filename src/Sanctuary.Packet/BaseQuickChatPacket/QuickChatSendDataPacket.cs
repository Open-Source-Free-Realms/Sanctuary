using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Packet;

public class QuickChatSendDataPacket : BaseQuickChatPacket, ISerializablePacket
{
    public new const short OpCode = 1;

    public Dictionary<int, QuickChatDefinition> QuickChats = new();

    public QuickChatSendDataPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(QuickChats);

        return writer.Buffer;
    }
}