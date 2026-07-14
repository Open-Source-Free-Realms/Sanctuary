using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sanctuary.Game.Interactions;

// One merchant ware: an item definition id and its coin cost. Persisted in the flat
// Resources/MerchantItems.json (the coin-store catalog of every ware + its price). Which wares each
// merchant SELLS is a separate composition file, Resources/MerchantSets.json (setId -> item ids).
public class MerchantWare
{
    public int Id { get; set; }
    public int Cost { get; set; }
}

// The two data files that define the shops. Both are read FRESH on every shop-open, so editing a
// cost or ware list (by hand, or via a future admin tool) takes effect on the next open - no restart.
// - MerchantItems.json : flat [{Id,Cost}] of EVERY ware across all sets = the coin-store catalog.
// - MerchantSets.json : {setId: [itemId,...]} = what each merchant subtype's set sells.
public static class MerchantStore
{
    public static readonly string FilePath = Path.Combine(ResourceManager.BaseDirectory, "MerchantItems.json");
    public static readonly string SetsFilePath = Path.Combine(ResourceManager.BaseDirectory, "MerchantSets.json");
    // Maps a merchant NPC name -> the setId it sells (its canonical wiki inventory). "*<subtype>"
    // keys (e.g. "*brawler") are the fallback used when an NPC name has no explicit entry.
    public static readonly string NpcSetsFilePath = Path.Combine(ResourceManager.BaseDirectory, "MerchantNpcSets.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object Gate = new();

    // The flat catalog (id + cost) of every merchant ware. Seeds on first use if the file is
    // missing (degraded fallback = the one captured set).
    public static List<MerchantWare> Load(IResourceManager resourceManager)
    {
        lock (Gate)
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    var wares = JsonSerializer.Deserialize<List<MerchantWare>>(File.ReadAllText(FilePath));
                    if (wares is { Count: > 0 })
                        return wares;
                }
                catch { /* corrupt - reseed */ }
            }
            return Seed(resourceManager);
        }
    }

    // Per-set composition: setId -> ware item ids.
    public static Dictionary<int, List<int>> LoadSets(IResourceManager resourceManager)
    {
        lock (Gate)
        {
            if (File.Exists(SetsFilePath))
            {
                try
                {
                    var sets = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(File.ReadAllText(SetsFilePath));
                    if (sets is { Count: > 0 })
                        return sets;
                }
                catch { /* corrupt - reseed */ }
            }
            return SeedSets(resourceManager);
        }
    }

    // NPC name -> setId (the merchant's canonical inventory). Read fresh so edits apply on next
    // shop-open. Returns an empty map if the file is missing (callers fall back to subtype/468).
    public static Dictionary<string, int> LoadNpcSets()
    {
        lock (Gate)
        {
            if (File.Exists(NpcSetsFilePath))
            {
                try
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(NpcSetsFilePath));
                    if (map is { Count: > 0 })
                        return map;
                }
                catch { /* corrupt - fall through to empty */ }
            }
            return new Dictionary<string, int>();
        }
    }

    // The ware ids a given merchant set sells. Falls back to the chef/captured set (468), then the
    // whole catalog, so a merchant always has something to sell.
    public static List<int> WaresForSet(IResourceManager resourceManager, int setId)
    {
        var sets = LoadSets(resourceManager);
        if (sets.TryGetValue(setId, out var wares) && wares.Count > 0)
            return wares;
        if (sets.TryGetValue(468, out var chef) && chef.Count > 0)
            return chef;
        return Load(resourceManager).Select(w => w.Id).ToList();
    }

    // Admin-set cost for an item id (from the flat catalog), else the item's definition cost.
    public static int CostFor(IResourceManager resourceManager, int itemId)
    {
        var ware = Load(resourceManager).FirstOrDefault(w => w.Id == itemId);
        if (ware is not null)
            return ware.Cost;
        return resourceManager.ClientItemDefinitions.TryGetValue(itemId, out var def) ? def.Cost : 0;
    }

    // Ensure both data files exist (seeded from item base costs) before any shop opens.
    public static void EnsureSeeded(IResourceManager resourceManager)
    {
        lock (Gate)
        {
            if (!File.Exists(FilePath)) Seed(resourceManager);
            if (!File.Exists(SetsFilePath)) SeedSets(resourceManager);
        }
    }

    private static List<MerchantWare> Seed(IResourceManager resourceManager)
    {
        var wares = ShopInteraction.MerchantWares
            .Select(id => new MerchantWare
            {
                Id = id,
                Cost = resourceManager.ClientItemDefinitions.TryGetValue(id, out var def) ? def.Cost : 0
            })
            .ToList();
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(wares, JsonOptions)); } catch { }
        return wares;
    }

    private static Dictionary<int, List<int>> SeedSets(IResourceManager resourceManager)
    {
        // Degraded fallback: the one captured set (468) only. The real per-subtype sets ship in
        // Resources/MerchantSets.json (derived from the FreeRealms wiki vendor inventories).
        var sets = new Dictionary<int, List<int>> { [468] = ShopInteraction.MerchantWares.ToList() };
        try { File.WriteAllText(SetsFilePath, JsonSerializer.Serialize(sets, JsonOptions)); } catch { }
        return sets;
    }
}
