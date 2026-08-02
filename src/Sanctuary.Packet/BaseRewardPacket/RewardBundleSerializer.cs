using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// One item shown in a reward bundle preview (the offer / turn-in "Show Details" panel and the
// coins/stars celebration). Maps to a RewardBundleEntryItem (entry type 1).
public class RewardBundleItem
{
    // Icon asset id (ClientItemDefinition.Icon.Id) - entry field @0x04, the shown icon.
    public int IconId;

    // Name text id (ClientItemDefinition.NameId) - entry field @0x10, resolved to the label.
    public int NameId;

    // Quantity - entry field @0x20. A count of 1 hides the "xN" label (retail behaviour).
    public int Count = 1;
}

// Writes a RewardBundleBase (client reader FUN_008e7930) - the shared reward blob embedded in the
// quest offer (QuestInfoPacket), turn-in (QuestEndPacket) and the 50/1 celebration (RewardBundlePacket).
// Beyond the fixed coins(+0x50)/stars(+0x48) scalars it carries a length-prefixed list of typed
// entries. Item rewards are RewardBundleEntryItem (type 1); the reward preview's PopulateRewards Lua
// reads them from the "BaseClient.Quest.Reward.Entries" data source and calls AddRewardItem with
// name = field @0x10, icon = field @0x04, count = field @0x20 (all live-confirmed via probes).
// The bundle's leading @0x74 byte is left false, so the item entry's optional trailing @0x44 int is
// absent (icon lives in @0x04, so the "extended" form isn't needed).
public static class RewardBundleSerializer
{
    public static void Write(PacketWriter writer, int coins, int experience, IReadOnlyList<RewardBundleItem>? items = null)
    {
        writer.Write(false);       // +0x74 bool ("extended" flag; false -> entries omit the @0x44 int)
        writer.Write(coins);       // +0x50 int (coins)
        writer.Write(experience);  // +0x48 int (job/profile experience, shown as XP in the preview)
        writer.Write(0);       // +0x4C int
        writer.Write(0);       // +0x54 int
        writer.Write(0);       // +0x6C int
        writer.Write(0);       // +0x70 int
        writer.Write(0f);      // +0x78 float
        writer.Write(0);       // +0x5C int
        writer.Write(0);       // +0x60 int
        writer.Write(0);       // guid pair 1, low
        writer.Write(0);       // guid pair 1, high
        writer.Write(0);       // guid pair 2, low
        writer.Write(0);       // guid pair 2, high
        writer.Write(0);       // +0x64 int
        writer.Write(0);       // +0x68 int

        // Entry list: count, then each typed entry (RewardBundleBase reader's factory loop).
        int count = items?.Count ?? 0;
        writer.Write(count);   // entry count
        if (items is not null)
        {
            foreach (var item in items)
            {
                writer.Write(1);              // entry type = 1 (RewardBundleEntryItem)
                writer.Write((byte)0);        // @0x0c bool
                writer.Write(item.IconId);    // @0x04 int - ICON asset id
                writer.Write(0);              // @0x08 int
                writer.Write(item.NameId);    // @0x10 int - NAME text id
                writer.Write(item.Count);     // @0x20 int - COUNT
                writer.Write(0);              // @0x24 int
                writer.Write(0);              // @0x28 int
                writer.Write(0);              // @0x2c string length = 0 (empty)
                writer.Write(0);              // @0x3c int
                writer.Write((byte)0);        // @0x40 bool
            }
        }

        writer.Write(0);       // +0x58 int
    }
}
