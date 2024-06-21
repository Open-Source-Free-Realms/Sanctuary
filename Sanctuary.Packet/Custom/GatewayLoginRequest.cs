using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GatewayLoginRequest : ISerializablePacket, IDeserializable<GatewayLoginRequest>
{
    public const byte OpCode = 100;

    public string? Challenge;

    public GameServerData Data = new();

    public string? ServerAddress;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Challenge);

        Data.Serialize(writer);

        writer.Write(ServerAddress);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GatewayLoginRequest value)
    {
        value = new GatewayLoginRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Challenge))
            return false;

        if (!value.Data.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ServerAddress))
            return false;

        return reader.RemainingLength == 0;
    }
}