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

    public new byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(StateId);
        writer.Write(GameId);
        writer.Write(GroupId);
        writer.Write(Scores.Count);

        foreach (var miniGameScoreInfo in Scores)
            miniGameScoreInfo.Serialize(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out MiniGameGameEndScorePacket miniGameGameEndScorePacket)
    {
        miniGameGameEndScorePacket = new();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != BaseMiniGamePacket.OpCode)
            return false;

        if (!reader.TryRead(out byte subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out miniGameGameEndScorePacket.StateId))
            return false;

        if (!reader.TryRead(out miniGameGameEndScorePacket.GameId))
            return false;

        if (!reader.TryRead(out miniGameGameEndScorePacket.GroupId))
            return false;

        if (!reader.TryRead(out int scoreCount) || scoreCount < 0 || scoreCount > reader.RemainingLength / 24)
            return false;

        for (var i = 0; i < scoreCount; i++)
        {
            var miniGameScoreInfo = new MiniGameScoreInfo();

            if (!miniGameScoreInfo.TryRead(ref reader))
                return false;

            miniGameGameEndScorePacket.Scores.Add(miniGameScoreInfo);
        }

        return reader.RemainingLength == 0;
    }
}
