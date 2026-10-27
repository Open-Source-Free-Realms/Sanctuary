using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sent by the client to request a custom house name, and sent back
/// by the server with <see cref="Accepted"/> set to the result.
/// </summary>
public class ClientHousingPacketRequestName : BaseHousingPacket, ISerializablePacket, IDeserializable<ClientHousingPacketRequestName>
{
    public new const short OpCode = 17;

    public string Name = null!;

    public bool Accepted;

    public ClientHousingPacketRequestName() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Name);
        writer.Write(Accepted);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketRequestName value)
    {
        value = new ClientHousingPacketRequestName();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Name))
            return false;

        if (!reader.TryRead(out value.Accepted))
            return false;

        return reader.RemainingLength == 0;
    }
}