using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RatingPacketVoteReply : BaseRatingPacket, ISerializablePacket
{
    public new const byte OpCode = 18;

    public ulong Guid;

    public bool Success;

    public RatingPacketVoteReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(Success);

        return writer.Buffer;
    }
}