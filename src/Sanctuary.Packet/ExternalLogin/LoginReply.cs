using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class LoginReply : ISerializablePacket
{
    public const byte OpCode = 2;

    public bool LoggedIn;
    public int Status;
    // Membership no longer exists. The client still locks member content behind this flag, so everyone is a member.
    public bool IsMember = true;

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