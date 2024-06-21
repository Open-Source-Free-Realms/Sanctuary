using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class CharacterSelectInfoReply : ISerializablePacket
{
    public const byte OpCode = 12;

    public int Status;
    public bool CanBypassServerLock;

    public List<EntityDetails> Entities = new();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Status);
        writer.Write(CanBypassServerLock);

        writer.Write(Entities);

        return writer.Buffer;
    }
}