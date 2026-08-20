using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.GameCommerce;

namespace Sanctuary.Game.Resources;

public static class HousingItemDefinitionGenerator
{
    private const int IndoorFurnitureGroupId = 119;
    private const int OutdoorFurnitureGroupId = 123;
    private const int FlooringGroupId = 141;
    private const int SurfaceGroupId = 142;

    private readonly record struct GeneratedFixture(string ModelName, int CategoryId, string TextureAlias = "");

    private static readonly IReadOnlyDictionary<int, GeneratedFixture> ExplicitFixtureDefinitions =
        new Dictionary<int, GeneratedFixture>
        {
            [11494] = new("farming_cropduster_01.adr", 57),
            [15971] = new("scarecrow_m_basic.adr", 57),
            [17601] = new("hsg_playerstudio_gazebo_01.adr", 57),
            [22940] = new("hsg_chair_spooky_01.adr", 53),
            [22941] = new("hsg_clock_spooky_01.adr", 54),
            [22942] = new("hsg_coffee_table_spooky_01.adr", 53),
            [22943] = new("hsg_painting_spooky_01.adr", 52),
            [22944] = new("hsg_sofa_spooky_01.adr", 53),
            [27942] = new("hsg_bed_lodge_01.adr", 53, "fun-hsg_bed_lodge_01-L1"),
            [27944] = new("hsg_dining_table_lodge_01.adr", 53, "fun-hsg_dining_table_lodge_01-L1"),
            [27945] = new("hsg_sofa_lodge_01.adr", 53, "fun-hsg_sofa_lodge_01-L1"),
            [27946] = new("hsg_rug_lodge_01.adr", 54, "fun-hsg_rug_lodge_01-L1"),
            [38800] = new("hsg_bed_large_biker_01.adr", 53),
            [38801] = new("hsg_chair_biker_01.adr", 53),
            [38802] = new("hsg_dining_table_biker_01.adr", 53),
            [38803] = new("hsg_floor_lamp_biker_01.adr", 54),
            [38804] = new("hsg_loveseat_biker_01.adr", 53),
            [38805] = new("hsg_mounted_head_drake_01.adr", 52),
            [38806] = new("hsg_cactus_barrel_01.adr", 57),
            [38807] = new("hsg_cactus_beavertail_01.adr", 57),
            [38808] = new("hsg_cactus_seguaro_01.adr", 57),
            [38809] = new("hsg_land_bridge_01.adr", 57),
            [38810] = new("hsg_shrubs_02.adr", 57),
            [38811] = new("hsg_shrubs_01.adr", 57),
            [38812] = new("hsg_sunstone_rock_01.adr", 57),
            [38813] = new("hsg_tree_savanna_01.adr", 57),
            [38814] = new("hsg_tree_savanna_02.adr", 57),
            [78134] = new("hsg_astro_jump_briarwood_fort_01.adr", 57, "fun-briarwoodastrojump-L1"),
            [78173] = new("hsg_mounted_head_01.adr", 52)
        };

    private static readonly IReadOnlyDictionary<int, int> ExplicitDefinitionSources = new Dictionary<int, int>
    {
        [16193] = 16907,
        [16194] = 16908,
        [16196] = 16912
    };

    public static int AddMissingDefinitions(
        ClientItemDefinitionCollection itemDefinitions,
        StoreDefinitionCollection stores)
    {
        var added = 0;

        foreach (var bundle in stores.Values
            .SelectMany(store => store.Bundles.Values)
            .Where(bundle => IsHousingGroup(bundle.CategoryGroupId)))
        {
            foreach (var entry in bundle.Entries)
            {
                if (entry.MarketingItemId <= 0 || itemDefinitions.ContainsKey(entry.MarketingItemId))
                    continue;

                var definition = CreateDefinition(itemDefinitions, bundle, entry.MarketingItemId);
                if (definition is not null && itemDefinitions.TryAdd(definition.Id, definition))
                    added++;
            }
        }

        return added;
    }

    private static ClientItemDefinition? CreateDefinition(
        ClientItemDefinitionCollection itemDefinitions,
        AppStoreBundleDefinition bundle,
        int marketingItemId)
    {
        if (bundle.CategoryGroupId is FlooringGroupId or SurfaceGroupId)
            return CreateSurfaceDefinition(bundle, marketingItemId);

        if (marketingItemId == 10451)
            return CreatePartyPoolDefinition(bundle);

        if (marketingItemId == 76878 && itemDefinitions.TryGetValue(16866, out var juiceBarSource))
            return CreateVipJuiceBarDefinition(juiceBarSource, bundle);

        if (ExplicitFixtureDefinitions.TryGetValue(marketingItemId, out var generatedFixture))
            return CreateFixtureDefinition(bundle, marketingItemId, generatedFixture);

        if (ExplicitDefinitionSources.TryGetValue(marketingItemId, out var sourceId) &&
            itemDefinitions.TryGetValue(sourceId, out var explicitSource))
        {
            return CloneForBundle(explicitSource, bundle, marketingItemId);
        }

        var source = itemDefinitions.Values.FirstOrDefault(candidate =>
                candidate.Type != 16 &&
                IsFixtureDefinitionSource(candidate) &&
                !string.IsNullOrWhiteSpace(bundle.Comment) &&
                string.Equals(candidate.Comment, bundle.Comment, StringComparison.OrdinalIgnoreCase))
            ?? itemDefinitions.Values.FirstOrDefault(candidate =>
                candidate.NameId == bundle.NameId && IsFixtureDefinitionSource(candidate));

        return source is null ? null : CloneForBundle(source, bundle, marketingItemId);
    }

    private static ClientItemDefinition CreateFixtureDefinition(
        AppStoreBundleDefinition bundle,
        int marketingItemId,
        GeneratedFixture fixture)
    {
        return new ClientItemDefinition
        {
            Comment = bundle.Comment,
            Id = marketingItemId,
            Type = 1,
            NameId = bundle.NameId,
            DescriptionId = bundle.DescriptionId,
            Icon = CreateIcon(bundle),
            Cost = bundle.Price,
            MaxStackSize = -1,
            NoTrade = true,
            MinProfileRank = 1,
            TintAlias = "dyetint",
            TextureAlias = fixture.TextureAlias,
            MemberDiscount = bundle.MemberDiscount,
            ModelName = fixture.ModelName,
            CategoryId = fixture.CategoryId,
            NoSale = true,
            Rarity = 3,
            IsTintable = bundle.IsTintable
        };
    }

    private static bool IsFixtureDefinitionSource(ClientItemDefinition candidate)
    {
        if (candidate.Type is 17 or 29)
            return true;

        if (candidate.Type != 1 || string.IsNullOrWhiteSpace(candidate.ModelName))
            return false;

        return candidate.ModelName.StartsWith("hsg_", StringComparison.OrdinalIgnoreCase) ||
            candidate.ModelName.StartsWith("mkt_boombox", StringComparison.OrdinalIgnoreCase);
    }

    private static ClientItemDefinition CreatePartyPoolDefinition(AppStoreBundleDefinition bundle)
    {
        return new ClientItemDefinition
        {
            Comment = bundle.Comment ?? "Party Pool",
            Id = 10451,
            Type = 1,
            NameId = bundle.NameId,
            DescriptionId = bundle.DescriptionId,
            Icon = CreateIcon(bundle),
            Cost = bundle.Price,
            MaxStackSize = -1,
            NoTrade = true,
            MinProfileRank = 1,
            TintAlias = "dyetint",
            TextureAlias = "vip-pool-L1",
            MemberDiscount = bundle.MemberDiscount,
            ModelName = "hsg_vip_party_pool_01.agr",
            CategoryId = 57,
            NoSale = true,
            Rarity = 3,
            ForceDisablePreview = true,
            IsTintable = true
        };
    }

    private static ClientItemDefinition CreateVipJuiceBarDefinition(
        ClientItemDefinition source,
        AppStoreBundleDefinition bundle)
    {
        var definition = CloneForBundle(source, bundle, 76878);
        definition.ModelName = "hsg_vip_juicebar_01.adr";
        definition.TextureAlias = "vip-juicebar-L1";
        definition.TintAlias = "dyetint";
        definition.CategoryId = 57;
        return definition;
    }

    private static ClientItemDefinition CreateSurfaceDefinition(AppStoreBundleDefinition bundle, int marketingItemId)
    {
        var categoryId = bundle.CategoryGroupId == FlooringGroupId
            ? 51
            : ResolveSurfaceCategory(bundle.Comment);

        return new ClientItemDefinition
        {
            Comment = bundle.Comment,
            Id = marketingItemId,
            Type = 17,
            NameId = bundle.NameId,
            DescriptionId = bundle.DescriptionId,
            Icon = CreateIcon(bundle),
            Cost = bundle.Price,
            MaxStackSize = -1,
            NoTrade = true,
            MinProfileRank = 1,
            TintAlias = "dyetint",
            TextureAlias = "customization",
            MemberDiscount = bundle.MemberDiscount,
            Param1 = 2,
            CategoryId = categoryId,
            NoSale = true,
            Rarity = 3,
            IsTintable = true,
            ModelName = string.Empty
        };
    }

    private static int ResolveSurfaceCategory(string? comment)
    {
        if (comment?.Contains("ceiling", StringComparison.OrdinalIgnoreCase) == true)
            return 60;

        if (comment?.Contains("roof", StringComparison.OrdinalIgnoreCase) == true ||
            comment?.Contains("shingle", StringComparison.OrdinalIgnoreCase) == true)
        {
            return 55;
        }

        return 59;
    }

    private static ClientItemDefinition CloneForBundle(
        ClientItemDefinition source,
        AppStoreBundleDefinition bundle,
        int marketingItemId)
    {
        return new ClientItemDefinition
        {
            Comment = bundle.Comment ?? source.Comment,
            Id = marketingItemId,
            Type = source.Type,
            NameId = bundle.NameId != 0 ? bundle.NameId : source.NameId,
            DescriptionId = bundle.DescriptionId != 0 ? bundle.DescriptionId : source.DescriptionId,
            Icon = CreateIcon(bundle, source.Icon),
            Unknown = source.Unknown,
            Unknown2 = source.Unknown2,
            ActivatableAbilityId = 0,
            PassiveAbilityId = 0,
            Cost = bundle.Price != 0 ? bundle.Price : source.Cost,
            Class = source.Class,
            MaxStackSize = source.MaxStackSize,
            ProfileOverride = source.ProfileOverride,
            Slot = source.Slot,
            NoTrade = source.NoTrade,
            SingleUse = source.SingleUse,
            ModelName = source.ModelName,
            GenderUsage = source.GenderUsage,
            TextureAlias = source.TextureAlias,
            CategoryId = source.CategoryId,
            MembersOnly = source.MembersOnly,
            NonMiniGame = source.NonMiniGame,
            NoSale = source.NoSale,
            WeaponTrailEffectId = source.WeaponTrailEffectId,
            CompositeEffectId = source.CompositeEffectId,
            PowerRating = source.PowerRating,
            MinProfileRank = source.MinProfileRank,
            Rarity = source.Rarity,
            TintAlias = source.TintAlias,
            IsTintable = source.IsTintable,
            ForceDisablePreview = source.ForceDisablePreview,
            MemberDiscount = bundle.MemberDiscount != 0 ? bundle.MemberDiscount : source.MemberDiscount,
            RaceSetId = source.RaceSetId,
            VipRankRequired = source.VipRankRequired,
            ClientEquipReqSetId = source.ClientEquipReqSetId,
            ResellValue = source.ResellValue,
            Param1 = source.Param1,
            Param2 = source.Param2,
            Unknown5 = source.Unknown5,
            Stats = source.Stats.ToDictionary(pair => pair.Key, pair => pair.Value),
            Abilities = []
        };
    }

    private static IconData CreateIcon(AppStoreBundleDefinition bundle, IconData? fallback = null)
    {
        _ = int.TryParse(bundle.Image.Image, out var iconId);
        _ = int.TryParse(bundle.Image.Tint, out var tintId);

        return new IconData
        {
            Id = iconId != 0 ? iconId : fallback?.Id ?? 0,
            TintId = tintId != 0 ? tintId : fallback?.TintId ?? 0
        };
    }

    private static bool IsHousingGroup(int categoryGroupId)
    {
        return categoryGroupId is IndoorFurnitureGroupId or OutdoorFurnitureGroupId or FlooringGroupId or SurfaceGroupId;
    }
}
