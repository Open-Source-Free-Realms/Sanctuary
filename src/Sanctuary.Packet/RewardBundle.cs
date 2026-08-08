using System.Collections.Generic;
using System.Linq;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// One prize row inside a RewardBundle. The client surfaces these in two places, both fed from the
// same preview bundle: the encounter offer popup's prize list (up to 4 non-hidden rows) and the
// victory screen's loot-wheel slices (hidden rows still render as slices).
public sealed class RewardEntry
{
    public int Type = 1;        // 1 = ITEM
    public bool Hidden;
    public int IconId;          // ClientItemDefinitions Icon.Id
    public int TintId;          // ClientItemDefinitions Icon.TintId
    public int NameId;          // item name string id
    public int Quantity = 1;
    public int ItemDefId;       // wire Param1 = the ClientItemDefinitions item id
    public int Param2;

    /// <summary>Optional per-entry inventory item guid tail (the player's item row id, not the def
    /// id). The bundle's lead byte gates these for every entry at once.</summary>
    public int? TailItemGuid;
}

// Shared RewardBundleBase serializer:
//   byte U1 ("entries carry ItemGuid tails") · int32 ×9 (U2 = coins, U3 = XP, U8 = 1.0f) ·
//   int32 ×4 (two id pairs) · int32 U13 (icon override; -1 = defer to entry[0]) · int32 U14 (name
//   override; -1 = entry[0]) · int32 entryCount · entries · int32 U15.
//   Entry: int32 type · byte Hidden · int32 IconId · int32 TintId · int32 NameId · int32 Quantity ·
//   int32 Param1 (item def id) · int32 Param2 · string · int32 text color · byte members-only ·
//   [int32 ItemGuid iff bundle U1].
public static class RewardBundle
{
    public static void Write(PacketWriter writer, IReadOnlyList<RewardEntry> entries,
        int coins = 0, int xp = 0, int iconOverride = -1, int nameOverride = -1, int unknown15 = 0)
    {
        var tails = entries.Any(e => e.TailItemGuid.HasValue);

        writer.Write(tails);
        writer.Write(coins);
        writer.Write(xp);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1.0f);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0); writer.Write(0);
        writer.Write(0); writer.Write(0);
        writer.Write(iconOverride);
        writer.Write(nameOverride);
        writer.Write(entries.Count);
        foreach (var e in entries)
        {
            writer.Write(e.Type);
            writer.Write(e.Hidden);
            writer.Write(e.IconId);
            writer.Write(e.TintId);
            writer.Write(e.NameId);
            writer.Write(e.Quantity);
            writer.Write(e.ItemDefId);
            writer.Write(e.Param2);
            writer.Write((string?)null);
            writer.Write(0);              // item text color (0 = default)
            writer.Write(false);          // members only
            if (tails)
                writer.Write(e.TailItemGuid ?? 0);
        }
        writer.Write(unknown15);
    }
}
