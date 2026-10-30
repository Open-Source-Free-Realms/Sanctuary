using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class MiniGameGameEndScorePacket : BaseMiniGamePacket, ISerializablePacket, IDeserializable<MiniGameGameEndScorePacket>
{
    public new const byte OpCode = 47;

    public List<MiniGameScoreInfo> Scores { get; } = [];

    public MiniGameGameEndScorePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Scores);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameGameEndScorePacket miniGameGameEndScorePacket)
    {
        miniGameGameEndScorePacket = new();

        var reader = new PacketReader(data);

        if (!miniGameGameEndScorePacket.TryRead(ref reader))
            return false;

        if (!reader.TryReadList(out List<MiniGameScoreInfo> scores))
            return false;

        miniGameGameEndScorePacket.Scores.AddRange(scores);

        return reader.RemainingLength == 0;
    }
}
