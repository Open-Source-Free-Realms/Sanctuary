using System;
using System.Linq;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class TradeItemRules
{
    public static bool CanTrade(ItemInstance item, ClientItemDefinition definition) =>
        !definition.NoTrade && item.AbilityCount == 0
        && definition.Type != ItemDefinitionHelper.PetItemType
        && definition.Type != ItemDefinitionHelper.MountItemType
        && definition.Type != ItemDefinitionHelper.HousingItemType;

    public static int AvailableCount(int count, int consumed, bool profileEquipped, int equippedPets) =>
        (int)Math.Clamp((long)count - Math.Max(0, consumed) - (profileEquipped ? 1 : 0) - equippedPets, 0, int.MaxValue);

    // The seven persisted utility slots and equipped item list define pet equipment.
    // UnknownItemGuid1 has no equipment mapping and is never populated by the server.
    public static bool PetUsesItem(int itemId, bool inEquipmentList, params int[] utilityItems) =>
        inEquipmentList || utilityItems.Contains(itemId);
}
