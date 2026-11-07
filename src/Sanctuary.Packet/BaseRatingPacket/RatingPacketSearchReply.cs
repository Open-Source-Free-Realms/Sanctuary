using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class RatingPacketSearchReply : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 13;

    public ulong Guid;

    public string? System;

    public List<RatingDataEntry> Results = [];

    public RatingPacketSearchReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(System);

        writer.Write(Results);

        return writer.Buffer;
    }
}