using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class CommandPacketInteractionList : BaseCommandPacket, ISerializablePacket, IDeserializable<CommandPacketInteractionList>
{
    public new const short OpCode = 9;

    public InteractionList List = new();

    public bool Unknown;

    public CommandPacketInteractionList() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        List.Serialize(writer);

        writer.Write(Unknown);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketInteractionList value)
    {
        value = new CommandPacketInteractionList();
        var reader = new PacketReader(data);
        if (!value.TryRead(ref reader) || !value.List.TryRead(ref reader) || !reader.TryRead(out value.Unknown))
            return false;
        return reader.RemainingLength == 0;
    }

}