using System.Collections.Generic;
using System.Linq;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// One prize row inside a RewardBundle. The client surfaces these in TWO places, both fed from the SAME
// preview bundle (decompiled UI Lua): the NPC-talk offer popup's prize list (MinigameStartScreen reads
// "BaseClient.MiniGame.RewardPreview.Entries", shows up to 4 NON-hidden rows) and the victory score
// screen's LOOT WHEEL slices (ScoreScreen:PopulateLootWheel reads the same DS). Hidden=true rows are
// skipped by the popup list but still land in the DS (and still render as wheel slices).
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

    // Optional per-entry INVENTORY item guid tail (the player's item row id, not the def id).
    // IDA-verified 2026-07-04: the bundle's LEAD BYTE (not entry U9) gates these — the bundle reader
    // (sub_8E7930) pushes it into every entry, and RewardBundleEntryItem then reads a 4-byte ItemGuid
    // after the base. The live post-wheel grant display (RewardBundlePacket idx 38142) used this.
    public int? TailItemGuid;

    // SERVER-SIDE ONLY (never put on the wire, never sent to the client — the client resolves NameId
    // itself). The item's plain real name, for building the blue "You receive 1 X" toast text server-side
    // (see BaseMiniGamePacketHandler.HandleLootWheelStopped / EncounterArenaZone.GrantBonusGoalReward) -
    // ClientItemDefinition has no loaded name/comment field we could look this up from at runtime.
    public string DisplayName = "";
}

// Shared RewardBundleBase serializer — wire format ground-truthed against the real 04-01 packets AND the
// client readers (drafts/reward-bundle-format.md has the full decode):
//   byte U1 ("entries carry ItemGuid tails") · int32 ×9 U2..U10 (U2=Num Coins, U3=Experience — the
//   IDA-verified RewardDataSource columns; U8=1.0f in every live bundle) · int32 ×2 pairA · int32 ×2 pairB ·
//   int32 U13 (icon override; -1 = defer to entry[0]) · int32 U14 (name override; -1 = entry[0]) ·
//   int32 entryCount · entries · int32 U15.
//   Entry: int32 type · byte Hidden · int32 IconId · int32 TintId · int32 NameId · int32 Quantity ·
//   int32 Param1(item def id) · int32 Param2 · string · int32 U8("Item Text Color") ·
//   byte U9("Members Only") · [int32 ItemGuid iff bundle U1].
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
