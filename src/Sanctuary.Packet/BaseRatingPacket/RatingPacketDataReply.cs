using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class RatingPacketDataReply : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 5;

    public ulong Guid;

    public string? System;

    public Dictionary<uint, RatingDataEntry> Entries = [];

    public RatingListType ListType;

    public RatingPacketDataReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(System);

        writer.Write(Entries);

        writer.Write(ListType);

        return writer.Buffer;
    }
}