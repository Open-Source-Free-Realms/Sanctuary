using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The gateway's answer to <see cref="GatewayCharacterReleaseRequest"/>. <see cref="Released"/> is true when the
/// character had no session, or its session was silent long enough and has been dropped.
/// </summary>
public class GatewayCharacterReleaseReply : ISerializablePacket, IDeserializable<GatewayCharacterReleaseReply>
{
    public const byte OpCode = 105;

    public uint RequestId;

    public bool Released;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(RequestId);

        writer.Write(Released);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GatewayCharacterReleaseReply value)
    {
        value = new GatewayCharacterReleaseReply();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.RequestId) || !reader.TryRead(out value.Released))
            return false;

        return reader.RemainingLength == 0;
    }
}
