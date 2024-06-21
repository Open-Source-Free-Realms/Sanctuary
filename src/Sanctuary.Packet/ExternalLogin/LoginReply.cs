using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class LoginReply : ISerializablePacket
{
    public const byte OpCode = 2;

    public bool LoggedIn;
    public int Status;
    public bool IsMember;

    public byte[] Payload = Array.Empty<byte>();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(LoggedIn);
        writer.Write(Status);
        writer.Write(IsMember);

        writer.WritePayload(Payload);

        return writer.Buffer;
    }
}