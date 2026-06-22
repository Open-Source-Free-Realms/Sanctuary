using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class QuickChatSendTellPacket : QuickChatSendChatPacketBase, ISerializablePacket, IDeserializable<QuickChatSendTellPacket>
{
    public new const short OpCode = 2;

    public string ToName;

    public ulong Unknown;

    public QuickChatSendTellPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Unknown);

        writer.Write(ToName);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out QuickChatSendTellPacket value)
    {
        value = new QuickChatSendTellPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.ToName))
            return false;

        return reader.RemainingLength == 0;
    }
}