using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// The victory-screen loot wheel flow:
//   1) S2C op39 sub45 (this packet) — base header + one RewardBundle. The client matches the bundle's
//      first entry's NameId against the stored PREVIEW bundle rows; the matching row becomes the
//      wheel's landing slice. No entry: Coins > 0 lands on the coins slice. The spin animation is
//      pure theater — the outcome is whatever this packet says.
//   2) The player clicks spin; the wheel animates to the stored slice.
//   3) C2S op39 sub46 (base header only) — the client reports the wheel stopped; the server grants
//      the prize.
public class MiniGameLootWheelSetItemToLandOnPacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 45;

    /// <summary>The landed prize (single entry; only NameId matters for slice selection).
    /// Leave empty and set Coins to land on the coins slice.</summary>
    public List<RewardEntry> Entries = [];

    public int Coins;

    public int Unknown15 = 957; // constant across observed wheel bundles

    public MiniGameLootWheelSetItemToLandOnPacket() : base(OpCode, -1, -1, -1)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // all -1 = "current state"

        var iconOverride = Entries.Count > 0 ? Entries[0].IconId : -1;
        var nameOverride = Entries.Count > 0 ? Entries[0].NameId : -1;
        RewardBundle.Write(writer, Entries, Coins, 0, iconOverride, nameOverride, Unknown15);

        return writer.Buffer;
    }
}

public sealed class MiniGameScoreRow
{
    public string Name = "";    // client string key, e.g. "scoreEnemiesDefeated"
    public int NameId = -1;     // -1 = the string key carries the label
    public int Order;           // display order: 0 enemies, 2 time bonus, 3 knockouts, 4 total
    public int Value = -1;      // e.g. enemies defeated; -1 = none (total row)
    public int Max = -1;        // e.g. knockouts 5 of Max 5; -1 = no max
    public int Points;          // score contribution shown right-aligned
}

// S2C op39 sub47 — the victory screen's score rows. Per row:
// [i32 len][ascii name][i32 NameId][i32 Order][i32 Value][i32 Max][i32 Points].
// The client appends its own scoreObjectives/scoreBonusObjectives rows from the MiniGameState.
public class MiniGameGameEndScorePacket : BaseMiniGamePacket, ISerializablePacket
{
    public new const byte OpCode = 47;

    public List<MiniGameScoreRow> Rows = [];

    public MiniGameGameEndScorePacket() : base(OpCode, -1, -1, -1)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Rows.Count);
        foreach (var row in Rows)
        {
            writer.Write(row.Name);
            writer.Write(row.NameId);
            writer.Write(row.Order);
            writer.Write(row.Value);
            writer.Write(row.Max);
            writer.Write(row.Points);
        }

        return writer.Buffer;
    }
}
