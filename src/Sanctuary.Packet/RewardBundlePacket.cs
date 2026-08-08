using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op50 sub1 — standalone reward-grant banner, sent right after the loot wheel stops. Two shapes:
// a CONTENTS grant (entries list, e.g. the items inside an opened pack) or a PRIZE banner
// (no entries, IconId/NameId = the won prize — the "you won X" display).
public class RewardBundlePacket : ISerializablePacket
{
    public const short OpCode = 50;
    public const byte SubOpCode = 1;

    public List<RewardEntry> Entries = [];

    public int Coins;
    public int Xp;

    /// <summary>Banner icon/name (-1 = defer to entry[0]).</summary>
    public int IconId = -1;
    public int NameId = -1;

    public int Unknown15;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        RewardBundle.Write(writer, Entries, Coins, Xp, IconId, NameId, Unknown15);

        return writer.Buffer;
    }
}
