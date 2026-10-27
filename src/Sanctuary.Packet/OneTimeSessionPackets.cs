using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The client asks for a one-time session token (e.g. type 41 when the daily wheel is opened) and waits for
/// <see cref="OneTimeSessionResponsePacket"/>.
/// </summary>
public class OneTimeSessionRequestPacket : IDeserializable<OneTimeSessionRequestPacket>
{
    public const short OpCode = 0x75;

    public int Type;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out OneTimeSessionRequestPacket value)
    {
        value = new OneTimeSessionRequestPacket();
        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Type))
            return false;

        return reader.RemainingLength == 0;
    }
}

/// <summary>
/// Layout from the client's reader: success, a session string, the request type and a second string.
/// </summary>
public class OneTimeSessionResponsePacket : ISerializablePacket
{
    public const short OpCode = 0x76;

    public bool Success;
    public string Session = string.Empty;
    public int Type;
    public string Url = string.Empty;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(Success);
        writer.Write(Session);
        writer.Write(Type);
        writer.Write(Url);

        return writer.Buffer;
    }
}
