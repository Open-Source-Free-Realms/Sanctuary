using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketItemDefinitionRequest : BasePlayerUpdatePacket, ISerializablePacket, IDeserializable<PlayerUpdatePacketItemDefinitionRequest>
{
    public new const short OpCode = 34;

    public int Id;

    /// <summary>
    /// This is a server-side only field.
    /// </summary>
    public byte[] Payload = Array.Empty<byte>();

    public PlayerUpdatePacketItemDefinitionRequest() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(Id);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PlayerUpdatePacketItemDefinitionRequest value)
    {
        value = new PlayerUpdatePacketItemDefinitionRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Id))
            return false;

        if (!reader.TryRead(out int payloadSize))
            return false;

        if (!reader.TryReadExact(payloadSize, out var payload))
            return false;

        value.Payload = new byte[payloadSize];

        if (!payload.TryCopyTo(value.Payload))
            return false;

        return reader.RemainingLength == 0;
    }
}