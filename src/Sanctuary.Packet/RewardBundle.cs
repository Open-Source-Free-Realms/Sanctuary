using System.Collections.Generic;
using System.Linq;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// One prize row inside a RewardBundle; feeds both the NPC-offer popup and the loot-wheel slices (Hidden rows skip the popup but still render as wheel slices).
public sealed class RewardEntry
{
    public int Type = 1;        // REWARDBUNDLE_TYPE: 1=ITEM
    public bool Hidden;
    public int IconId;          // ClientItemDefinitions Icon.Id
    public int TintId;          // ClientItemDefinitions Icon.TintId
    public int NameId;          // item name string id
    public int Quantity = 1;
    public int ItemDefId;       // wire Param1 = the ClientItemDefinitions item id ("Item Id" DS column)
    public int Param2;

    // Optional per-entry inventory item guid (player's item row id, not def id); presence is gated by the bundle's lead byte, not entry U9 (sub_8E7930).
    public int? TailItemGuid;

    // Server-side only, never sent on the wire - the item's plain name, used to build the "You receive 1 X" toast text.
    public string DisplayName = "";
}

// Shared RewardBundleBase serializer - wire format ground-truthed against real 04-01 packets and the client readers (see drafts/reward-bundle-format.md for the full decode).
public static class RewardBundle
{
    public static void Write(PacketWriter writer, IReadOnlyList<RewardEntry> entries,
        int coins = 0, int xp = 0, int iconOverride = -1, int nameOverride = -1, int unknown15 = 0)
    {
        var tails = entries.Any(e => e.TailItemGuid.HasValue);

        writer.Write(tails);                              // byte U1 — ItemGuid tails present
        writer.Write(coins);                              // U2  — Num Coins
        writer.Write(xp);                                 // U3  — Experience
        writer.Write(0);                                  // U4
        writer.Write(0);                                  // U5  (live carries small ints here; unread by the reward DS)
        writer.Write(0);                                  // U6  (live: the encounter id in wheel/grant bundles; unread)
        writer.Write(0);                                  // U7
        writer.Write(1.0f);                               // U8  — 1.0f in EVERY live bundle
        writer.Write(0);                                  // U9
        writer.Write(0);                                  // U10
        writer.Write(0); writer.Write(0);                 // pairA (live: a session guid; unread by the applies we use)
        writer.Write(0); writer.Write(0);                 // pairB
        writer.Write(iconOverride);                       // U13 — -1 = "use entry[0]'s icon" (client getter 0x1039D30)
        writer.Write(nameOverride);                       // U14 — -1 = "use entry[0]'s name"
        writer.Write(entries.Count);
        foreach (var e in entries)
        {
            writer.Write(e.Type);                         // int32 type — int32 ON THE WIRE (client reads a full int)
            writer.Write(e.Hidden);
            writer.Write(e.IconId);
            writer.Write(e.TintId);
            writer.Write(e.NameId);
            writer.Write(e.Quantity);
            writer.Write(e.ItemDefId);                    // Param1 = ClientItemDefinitions id
            writer.Write(e.Param2);
            writer.Write((string?)null);                  // string (int32 len 0)
            writer.Write(0);                              // int32 U8 "Item Text Color" (0 = default)
            writer.Write(false);                          // byte U9 "Members Only"
            if (tails)
                writer.Write(e.TailItemGuid ?? 0);        // 4-byte ItemGuid tail (bundle U1 gates it)
        }
        writer.Write(unknown15);                          // U15 (meaning unknown; live wheel bundles carry 957)
    }
}
