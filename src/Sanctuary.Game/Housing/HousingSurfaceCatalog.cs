using System;
using System.Collections.Generic;

namespace Sanctuary.Game.Housing;

public static class HousingSurfaceCatalog
{
    private static readonly IReadOnlyDictionary<string, int[]> Targets =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Key("hsg_hum_blackspore_01", "Floor")] = [5000, 5003, 5005, 5007],
            [Key("hsg_hum_blackspore_01", "Roof")] = [5001],
            [Key("hsg_hum_blackspore_01", "Wall")] = [5002, 5004, 5006, 5008]
        };

    private static readonly IReadOnlyDictionary<int, string> TextureOverrides =
        new Dictionary<int, string>
        {
            [22945] = "hsg_cust_floor_spooky_batcatghost_01.dds",
            [22946] = "hsg_cust_floor_spooky_pumpkin_01.dds",
            [22948] = "hsg_cust_wall_spooky_01.dds",
            [22950] = "hsg_cust_wall_spooky_batcatghost_01.dds",
            [22951] = "hsg_cust_wall_skullsandbows_01.dds",
            [27947] = "hsg_cust_floor_bw_cobblestone_01.dds",
            [27948] = "hsg_cust_floor_bw_darkgrass_01.dds",
            [27949] = "hsg_cust_floor_bw_dirt_01.dds",
            [27950] = "hsg_cust_floor_checker_tile_01.dds",
            [27951] = "hsg_cust_floor_fastfood_01.dds",
            [27952] = "hsg_cust_floor_fruit_01.dds",
            [27953] = "hsg_cust_floor_icecream_01.dds",
            [27954] = "hsg_cust_floor_pirate_01.dds",
            [27955] = "hsg_cust_floor_sg_cobblestone_01.dds",
            [27956] = "hsg_cust_floor_sg_dirt_01.dds",
            [27957] = "hsg_cust_floor_ss_deadgrass_01.dds",
            [27958] = "hsg_cust_floor_ss_sand_01.dds",
            [27959] = "hsg_cust_floor_tile_houndstooth_01.dds",
            [27960] = "hsg_cust_floor_tile_leopard_01.dds",
            [27961] = "hsg_cust_floor_tile_polkadot_01.dds",
            [27962] = "hsg_cust_floor_tile_polkadots_small_01.dds",
            [27963] = "hsg_cust_floor_tile_rainbow_01.dds",
            [27964] = "hsg_cust_floor_tile_spiral_01.dds",
            [27965] = "hsg_cust_floor_tile_star_02_01.dds",
            [27966] = "hsg_cust_floor_tile_tigerstripes_01.dds",
            [27967] = "hsg_cust_wall_checkers_01.dds",
            [27968] = "hsg_cust_wall_checkers_02.dds",
            [27969] = "hsg_cust_wall_cloudy_sky_01.dds",
            [27970] = "hsg_cust_wall_fastfood_01.dds",
            [27971] = "hsg_cust_wall_fruit_01.dds",
            [27972] = "hsg_cust_wall_houndstooth_01.dds",
            [27973] = "hsg_cust_wall_icecream_01.dds",
            [27974] = "hsg_cust_wall_leopardspots_01.dds",
            [27975] = "hsg_cust_wall_leopardspots_02.dds",
            [27976] = "hsg_cust_wall_polkadots_large_01.dds",
            [27977] = "hsg_cust_wall_polkadots_large_02.dds",
            [27978] = "hsg_cust_wall_polkadots_small_01.dds",
            [27979] = "hsg_cust_wall_polkadots_small_02.dds",
            [27980] = "hsg_cust_wall_rainbow_01.dds",
            [27981] = "hsg_cust_wall_seaside_sky_01.dds",
            [27982] = "hsg_cust_wall_skulls_01.dds",
            [27983] = "hsg_cust_wall_skulls_02.dds",
            [27984] = "hsg_cust_wall_snowhill_sky_01.dds",
            [27985] = "hsg_cust_wall_spirals_01.dds",
            [27986] = "hsg_cust_wall_spirals_02.dds",
            [27987] = "hsg_cust_wall_spirals_03.dds",
            [27988] = "hsg_cust_wall_spirals_04.dds",
            [27989] = "hsg_cust_wall_stars_01.dds",
            [27990] = "hsg_cust_wall_stars_and_stripes_01.dds",
            [27991] = "hsg_cust_wall_sweet_deserts_01.dds",
            [27992] = "hsg_cust_wall_tigerstripes_01.dds",
            [27993] = "hsg_cust_wall_tigerstripes_02.dds",
            [27994] = "hsg_cust_wall_underwater_01.dds",
            [27995] = "hsg_cust_wall_underwater_02.dds",
            [27996] = "hsg_cust_wall_underwater_sky_01.dds",
            [27997] = "hsg_cust_wall_wilds_sky_01.dds",
            [76776] = "hsg_cust_ceiling_texture_01.dds",
            [76777] = "hsg_cust_ceiling_woodhorizontal_01.dds",
            [76778] = "hsg_cust_floor_stone_01.dds",
            [76779] = "hsg_cust_floor_tile_01.dds",
            [76780] = "hsg_cust_floor_wooddiagonal_01.dds",
            [76781] = "hsg_cust_floor_woodhorizontal_01.dds",
            [76782] = "hsg_cust_floor_woodhorizontal_02.dds",
            [76783] = "hsg_cust_roof_hearts_01.dds",
            [76784] = "hsg_cust_roof_scales_01.dds",
            [76785] = "hsg_cust_roof_tiles_01.dds",
            [76786] = "hsg_cust_roof_woodhorizontal_01.dds",
            [76787] = "hsg_cust_wall_butterflies_01.dds",
            [76788] = "hsg_cust_wall_flames_01.dds",
            [76789] = "hsg_cust_wall_flowers_01.dds",
            [76790] = "hsg_cust_wall_plainpaint_01.dds",
            [76791] = "hsg_cust_wall_scales_01.dds",
            [76792] = "hsg_cust_wall_shanty_01.dds",
            [76793] = "hsg_cust_wall_space_01.dds",
            [76794] = "hsg_cust_wall_splatters_01.dds",
            [76795] = "hsg_cust_wall_stars_02.dds",
            [76796] = "hsg_cust_wall_stucco_01.dds",
            [76797] = "hsg_cust_wall_verticalstripe_01.dds",
            [76798] = "hsg_cust_wall_verticalstripe_02.dds",
            [76799] = "hsg_cust_wall_victorian_01.dds",
            [77424] = "hsg_cust_floor_woodhorizontal_01.dds",
            [77425] = "hsg_cust_wall_stars_02.dds",
            [17927] = "hsg_cust_wall_waldostripe_01.dds"
        };

    public static IReadOnlyList<int> GetTargetModelIds(string zoneName, string fixtureType)
    {
        return Targets.TryGetValue(Key(zoneName, fixtureType), out var modelIds)
            ? modelIds
            : [];
    }

    public static string GetTextureOverride(int itemDefinitionId)
    {
        return TextureOverrides.TryGetValue(itemDefinitionId, out var textureOverride)
            ? textureOverride
            : string.Empty;
    }

    private static string Key(string zoneName, string fixtureType)
    {
        return $"{zoneName.Trim()}|{fixtureType.Trim()}";
    }
}
