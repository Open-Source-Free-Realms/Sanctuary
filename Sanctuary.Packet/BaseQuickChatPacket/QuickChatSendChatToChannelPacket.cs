using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Packet;

public class QuickChatSendChatToChannelPacket : QuickChatSendChatPacketBase, ISerializablePacket, IDeserializable<QuickChatSendChatToChannelPacket>
{
    public new const short OpCode = 3;

    public ChatChannel Channel;

    public int AreaNameId;

    public ulong GuildGuid;

    public QuickChatSendChatToChannelPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Channel);

        writer.Write(AreaNameId);

        writer.Write(GuildGuid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out QuickChatSendChatToChannelPacket value)
    {
        value = new QuickChatSendChatToChannelPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Channel))
            return false;

        if (!reader.TryRead(out value.AreaNameId))
            return false;

        if (!reader.TryRead(out value.GuildGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}