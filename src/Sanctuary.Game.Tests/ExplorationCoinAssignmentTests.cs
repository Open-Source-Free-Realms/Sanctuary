using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class ExplorationCoinAssignmentTests
{
    // Names describe the reported physical locations; the numbers on coins are NPC definition IDs.
    [TestMethod]
    [DataRow(2090, 289, 2048, 408102, "Off the Wall")]
    [DataRow(2091, 289, 2049, 408103, "Down by the Sea")]
    [DataRow(2092, 289, 2053, 408108, "Oceanfront Property")]
    [DataRow(2093, 289, 2050, 408104, "Cliff Diving")]
    [DataRow(2094, 289, 2052, 408106, "Sea-cret Spot")]
    [DataRow(2096, 289, 2046, 408100, "Water off the Beach")]
    [DataRow(2097, 267, 1958, 386461, "Seaspray Hideaway")]
    [DataRow(2098, 267, 1959, 386462, "Under the Boardwalk")]
    [DataRow(2099, 267, 1957, 386460, "Bridging the Gap")]
    [DataRow(2101, 289, 2047, 408101, "Just Hanging Out")]
    [DataRow(2102, 267, 1956, 386459, "Beach Bum Storage")]
    [DataRow(2104, 270, 1969, 386473, "Snowy Peaks")]
    [DataRow(2105, 285, 2022, 408065, "Wugachug Lookout")]
    [DataRow(2106, 270, 1968, 386472, "The Ledge of Relaxation")]
    [DataRow(2108, 285, 2020, 408062, "Pine for Cover")]
    [DataRow(2112, 288, 2041, 408093, "Hollow Hiding Hole")]
    [DataRow(2118, 288, 2043, 408095, "A Relic View")]
    [DataRow(2120, 288, 2044, 408096, "Branching Out")]
    [DataRow(2121, 288, 2045, 408097, "Splortata's Timeout Spot")]
    [DataRow(4421, 269, 1966, 386470, "A Place to Pond-er")]
    [DataRow(4422, 269, 1939, 386344, "Gather Around the Campfire")]
    [DataRow(4424, 269, 1967, 386471, "Spore Falls")]
    [DataRow(4425, 269, 1964, 386468, "Xandril's Nook")]
    [DataRow(2302, 290, 2056, 116995, "Party Tent")]
    [DataRow(2303, 271, 1971, 386475, "A Sunny Harvest")]
    [DataRow(2304, 271, 1941, 386350, "Rooted Grip")]
    [DataRow(2305, 290, 2057, 408115, "I Stand Alone")]
    [DataRow(2306, 271, 1970, 386474, "Under the Over")]
    [DataRow(2307, 290, 2055, 408113, "Up a Tree")]
    [DataRow(2308, 290, 2058, 408116, "24hr Shade")]
    [DataRow(2309, 290, 2054, 408112, "A Hilly Hiding Place")]
    [DataRow(2473, 266, 1955, 386457, "Over the Rainbow")]
    [DataRow(2474, 291, 2060, 408120, "Spying on the Warriors")]
    [DataRow(2475, 291, 2059, 408119, "Sunset Cliffs")]
    [DataRow(2476, 291, 2062, 408122, "Shallow Water")]
    [DataRow(2477, 291, 2063, 408123, "Tree Cover")]
    [DataRow(2478, 291, 2064, 408124, "Left Falls")]
    [DataRow(2479, 291, 2061, 408121, "Up the Palace Walls")]
    [DataRow(4434, 284, 2018, 408058, "Perry's Hideaway")]
    [DataRow(4435, 265, 1948, 386449, "Perry's Lookout")]
    [DataRow(4436, 265, 1950, 386451, "Smitty's Hiding Place")]
    [DataRow(4437, 284, 2017, 408057, "Miner Difficulties")]
    [DataRow(4438, 284, 2016, 408056, "Hot Hangout")]
    [DataRow(4439, 265, 1935, 386279, "Beyond the Springs")]
    [DataRow(4440, 284, 2019, 408059, "Between Rocks and Trees")]
    [DataRow(4441, 284, 2014, 408054, "Penguin Path")]
    [DataRow(4442, 284, 2013, 408053, "Beyond the Nameless Peak")]
    [DataRow(4443, 265, 1949, 386450, "Penguin Point")]
    [DataRow(4444, 284, 2015, 408055, "Leafy Retreat")]
    [DataRow(34011, 284, 2012, 408052, "Melted Corner")]
    [DataRow(2125, 286, 2028, 408074, "A Cool Spot")]
    [DataRow(2126, 286, 2029, 408075, "Back Against the Wall")]
    [DataRow(2128, 286, 2026, 408072, "A Shady Area")]
    [DataRow(2129, 286, 2025, 408071, "Towering the Opening")]
    [DataRow(4445, 311, 2189, 8798, "Abandoned Mine")]
    [DataRow(4446, 311, 2188, 8797, "Wheelie Pete's Roadhouse")]
    [DataRow(4447, 312, 2202, 8811, "Vulture Alley")]
    [DataRow(4448, 311, 2191, 8800, "The Dig")]
    [DataRow(4449, 312, 2201, 8810, "Glimmerdeep Pass Overlook")]
    [DataRow(4450, 312, 2196, 8805, "Sandspear's Stand")]
    [DataRow(4451, 311, 2192, 8801, "Scorch Stream Falls")]
    [DataRow(4452, 312, 2197, 8806, "The Scrap Yard")]
    [DataRow(4453, 311, 2193, 8802, "Mystical Barrier")]
    [DataRow(4454, 312, 2203, 8812, "Sandscale Tribal Lands")]
    [DataRow(4455, 312, 2199, 8808, "Skullz Base Camp")]
    [DataRow(4461, 311, 2194, 8803, "Sunstone Arch")]
    [DataRow(4462, 312, 2200, 8809, "Drakewind Peak")]
    [DataRow(4463, 311, 2190, 8799, "Noggle's Retreat")]
    [DataRow(36196, 311, 2195, 8804, "The Great Rift")]
    [DataRow(36199, 312, 2198, 8807, "Drake Boneyard")]
    [DataRow(4465, 274, 1979, 386490, "Towering the Toll")]
    [DataRow(4467, 274, 1978, 386488, "Spying on Bandits")]
    [DataRow(4470, 274, 1981, 386492, "Tent Dent")]
    [DataRow(4471, 274, 1980, 386491, "Ancient Dig Site")]
    [DataRow(4473, 274, 1944, 386359, "Shepherd's Watch")]
    [DataRow(4481, 276, 1946, 386365, "Tin Man's Nest")]
    [DataRow(4482, 276, 1988, 386500, "Merry Notch")]
    [DataRow(4484, 276, 1989, 386501, "Gazing Beyond")]
    [DataRow(4485, 276, 1987, 386499, "A Path Less Traveled")]
    [DataRow(4488, 275, 1985, 386496, "Swimmer's Haven")]
    [DataRow(4489, 275, 1983, 386494, "Rushing Twin Falls")]
    [DataRow(4490, 275, 1984, 386495, "Rocking the River")]
    [DataRow(4491, 275, 1982, 386493, "Perfect Robgoblin View")]
    [DataRow(4492, 275, 1945, 386362, "Troll Scope")]
    [DataRow(4474, 273, 1975, 386483, "Serpent's Climb")]
    [DataRow(4477, 273, 1943, 386356, "Wildwood Shroud")]
    [DataRow(4479, 273, 1976, 386484, "Atop Spore Flow")]
    [DataRow(4480, 273, 1974, 386482, "Pixiewood Stare")]
    public void ReportedCoin_GrantsOnlyTheIntendedEntry(int npcDefinitionId, int collectionId, int entryId,
        int nameId, string location)
    {
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));
        var coins = new CollectionCoinDefinitionCollection(NullLogger.Instance);
        var collections = new CollectionDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(coins.Load(Path.Combine(resources, "CollectionCoins.json")));
        Assert.IsTrue(collections.Load(Path.Combine(resources, "Collections.json")));

        var coin = coins[npcDefinitionId];
        Assert.AreEqual(collectionId, coin.CollectionId, $"Coin #{npcDefinitionId}: {location}");
        Assert.AreEqual(entryId, coin.EntryId, $"Coin #{npcDefinitionId}: {location}");
        var collection = collections[coin.CollectionId].CreateClientCollection(1, new HashSet<int>(), new HashSet<int> { coin.EntryId });
        var collected = collection.Entries.Single(entry => entry.Collected);
        Assert.AreEqual(entryId, collected.Id);
        Assert.AreEqual(nameId, collected.NameId, $"Coin #{npcDefinitionId}: {location}");
    }

    [TestMethod]
    [DataRow(267)]
    [DataRow(269)]
    [DataRow(270)]
    [DataRow(271)]
    [DataRow(273)]
    [DataRow(274)]
    [DataRow(275)]
    [DataRow(284)]
    [DataRow(285)]
    [DataRow(286)]
    [DataRow(289)]
    [DataRow(290)]
    [DataRow(291)]
    [DataRow(311)]
    [DataRow(312)]
    public void ReassignedCollectionCatalog_HasExactlyOneCoinMappingPerEntry(int collectionId)
    {
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));
        var coins = new CollectionCoinDefinitionCollection(NullLogger.Instance);
        var collections = new CollectionDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(coins.Load(Path.Combine(resources, "CollectionCoins.json")));
        Assert.IsTrue(collections.Load(Path.Combine(resources, "Collections.json")));

        CollectionAssert.AreEquivalent(collections[collectionId].Entries.Select(entry => entry.Id).ToArray(),
            coins.Values.Where(coin => coin.CollectionId == collectionId).Select(coin => coin.EntryId).ToArray());
    }

    [TestMethod]
    public void TruncatedSoutheastReport_ProvisionallyUsesTheRemainingEntry()
    {
        // The report stops before #4478's target name. Peace of Mind still needs in-game confirmation.
        ReportedCoin_GrantsOnlyTheIntendedEntry(4478, 273, 1977, 386487, "Peace of Mind (provisional)");
    }

    [TestMethod]
    [DataRow(4435, 386170, "Rare Exploration Coin")]
    [DataRow(4438, 4412, "Elite Exploration Coin")]
    public void SnowhillCoin_DisplaysTheCorrectRarity(int npcDefinitionId, int nameId, string name)
    {
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Resources"));
        var npcs = new NpcDefinitionCollection(NullLogger.Instance);
        Assert.IsTrue(npcs.Load(Path.Combine(resources, "Npcs.json")));

        Assert.AreEqual(nameId, npcs[npcDefinitionId].NameId);
        Assert.AreEqual(name, npcs[npcDefinitionId].Name);
    }
}
