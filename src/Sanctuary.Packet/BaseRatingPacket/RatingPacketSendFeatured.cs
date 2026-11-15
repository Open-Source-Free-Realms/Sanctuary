using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class RatingPacketSendFeatured : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 22;

    public ulong Guid;

    public string? System;

    public RatingDataEntry Featured = new();

    public RatingPacketSendFeatured() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(System);

        Featured.Serialize(writer);

        return writer.Buffer;
    }
}