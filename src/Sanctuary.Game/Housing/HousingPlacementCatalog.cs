using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Housing;

public static class HousingPlacementCatalog
{
    public readonly record struct Entry(int ItemDefinitionId, int PlacementType, string AssetName);

    private static readonly Lazy<IReadOnlyDictionary<int, Entry>> Entries = new(Load);

    public static bool TryGet(int itemDefinitionId, out Entry entry)
    {
        return Entries.Value.TryGetValue(itemDefinitionId, out entry);
    }

    public static bool IsFixture(int itemDefinitionId)
    {
        return Entries.Value.ContainsKey(itemDefinitionId);
    }

    public static bool IsFixtureCustomization(ClientItemDefinition itemDefinition)
    {
        return itemDefinition.Type == 17 &&
            itemDefinition.Param1 == 2 &&
            itemDefinition.CategoryId is 51 or 55 or 59 or 60;
    }

    private static IReadOnlyDictionary<int, Entry> Load()
    {
        var entries = new Dictionary<int, Entry>();
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "HousingPlacementData.txt");
        if (!File.Exists(path))
            return entries;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                continue;

            var columns = line.Split('^', 3);
            if (columns.Length != 3 ||
                !int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemDefinitionId) ||
                !int.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var placementType) ||
                string.IsNullOrWhiteSpace(columns[2]))
            {
                continue;
            }

            entries[itemDefinitionId] = new Entry(itemDefinitionId, placementType, columns[2].Trim());
        }

        return entries;
    }
}
