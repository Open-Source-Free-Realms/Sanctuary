using System;

using Sanctuary.Game.Housing;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway;

public static class StoreInventoryPurchasePolicy
{
    public static bool IsSupported(ClientItemDefinition itemDefinition)
    {
        if (itemDefinition.Type == 16)
            return false;

        return itemDefinition.Type is 1 or 12 || IsHousingInventoryItem(itemDefinition);
    }

    public static bool IsHousingInventoryItem(ClientItemDefinition itemDefinition)
    {
        if (itemDefinition.Type == 16)
            return false;

        if (HousingPlacementCatalog.IsFixtureCustomization(itemDefinition))
            return true;

        if (itemDefinition.Type == 29)
            return true;

        if (itemDefinition.Type != 1)
            return false;

        if (HousingPlacementCatalog.IsFixture(itemDefinition.Id))
            return true;

        if (string.IsNullOrWhiteSpace(itemDefinition.ModelName))
            return false;

        return itemDefinition.ModelName.StartsWith("hsg_", StringComparison.OrdinalIgnoreCase);
    }

    public static int ResolveHousingTint(ClientItemDefinition itemDefinition, int requestedTint)
    {
        if (requestedTint == 0 && itemDefinition.CategoryId == 147 && itemDefinition.Icon.TintId > 0)
            return itemDefinition.Icon.TintId;

        return requestedTint;
    }
}
