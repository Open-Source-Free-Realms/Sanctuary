using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Login asks the gateway to drop a character's session if it has been silent for at least
/// <see cref="IdleThresholdMilliseconds"/>. The gateway answers with <see cref="GatewayCharacterReleaseReply"/>.
/// </summary>
public class GatewayCharacterReleaseRequest : ISerializablePacket, IDeserializable<GatewayCharacterReleaseRequest>
{
    public const byte OpCode = 104;

    public uint RequestId;

    public ulong Id;

    public uint IdleThresholdMilliseconds;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(RequestId);

        writer.Write(Id);

        writer.Write(IdleThresholdMilliseconds);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GatewayCharacterReleaseRequest value)
    {
        value = new GatewayCharacterReleaseRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.RequestId) || !reader.TryRead(out value.Id) || !reader.TryRead(out value.IdleThresholdMilliseconds))
            return false;

        return reader.RemainingLength == 0;
    }
}
