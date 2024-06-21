using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketSetChatBubbleColor : BaseCommandPacket, ISerializablePacket, IDeserializable<CommandPacketSetChatBubbleColor>
{
    public new const short OpCode = 18;

    public int ChatBubbleForegroundColor;
    public int ChatBubbleBackgroundColor;
    public int ChatBubbleSize;

    public ulong Guid;

    public CommandPacketSetChatBubbleColor() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(ChatBubbleForegroundColor);
        writer.Write(ChatBubbleBackgroundColor);
        writer.Write(ChatBubbleSize);

        writer.Write(Guid);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketSetChatBubbleColor value)
    {
        value = new CommandPacketSetChatBubbleColor();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ChatBubbleForegroundColor))
            return false;

        if (!reader.TryRead(out value.ChatBubbleBackgroundColor))
            return false;

        if (!reader.TryRead(out value.ChatBubbleSize))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}