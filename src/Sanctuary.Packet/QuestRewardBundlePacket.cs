using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op50 (RewardBase family) sub 1: the reward-earned celebration (coins + XP fly-in with sound) sent
// after a quest turn-in. Named distinctly from Sanctuary.Packet.BaseRewardPacket.RewardBundlePacket,
// which covers the collection-node pickup toast - same opcode family, different body shape.
public class QuestRewardBundlePacket : BaseRewardPacket, ISerializablePacket
{
    public const byte SubOpCode = 1;

    public List<RewardEntry> Entries = [];

    public int Coins;
    public int Xp;

    // Banner icon/name (-1 = defer to entry[0] — the client's U13/U14 fallback).
    public int IconId = -1;
    public int NameId = -1;

    public int Unknown15;

    public QuestRewardBundlePacket() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        RewardBundle.Write(writer, Entries, Coins, Xp, IconId, NameId, Unknown15);

        return writer.Buffer;
    }
}
