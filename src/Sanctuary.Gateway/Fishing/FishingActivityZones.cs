using System.Collections.Generic;
using System.Numerics;

namespace Sanctuary.Gateway.Fishing;

public readonly record struct FishingZoneConfig(
    string ZoneName,
    string? Sky,
    Vector4 SpawnPosition,
    Quaternion SpawnRotation,
    float UnderwaterBedX);

public static class FishingActivityZones
{
    public const int FishingCharacterState = 0x400000;

    // Each fishing map has a dedicated "Underwater_Bed" area (from <zone>Areas.xml) near
    // X≈55-74, Y≈-2..0, Z≈482-486. The client renders the underwater fishing scene there and
    // hardcodes the fish arena at Y=-8, Z=485; only the X is configurable (ZoneConfig.Unknown3).
    // So UnderwaterBedX MUST be the bed's X or the fish appears over the wrong (overworld) water.
    private static readonly Dictionary<int, FishingZoneConfig> ZonesByActivityId = new()
    {
        // Darklit Lagoon — bw_fishing_medpond UnderWater_Bed (74, -2, 482)
        [560] = new("bw_fishing_medpond", null, new Vector4(200f, 0f, 200f, 1f), Quaternion.Identity, 74f),
        // Brambleback's Bayou — bw_fishing_stream Underwater_Bed (58, -2, 484)
        [561] = new("bw_fishing_stream", null, new Vector4(200f, 0f, 200f, 1f), Quaternion.Identity, 58f),
        // Rainbow Lake — bw_fishing_medpond UnderWater_Bed (74, -2, 482)
        [562] = new("bw_fishing_medpond", null, new Vector4(200f, 0f, 200f, 1f), Quaternion.Identity, 74f),
        // Sacred Grove Shallows — sg_fishing_medpond Underwater_Bed (68, -2, 476)
        [563] = new("sg_fishing_medpond", null, new Vector4(435.05676f, -64.46508f, 370.70682f, 1f), Quaternion.Identity, 68f),
        // Wintery Basin — sh_fishing_medpond Underwater_Bed (55, 0, 486)
        [564] = new("sh_fishing_medpond", null, new Vector4(200f, 0f, 200f, 1f), Quaternion.Identity, 55f),
        // Frostbitten Banks — sh_fishing_stream Underwater_Bed (69, -1, 484)
        [565] = new("sh_fishing_stream", null, new Vector4(200f, 0f, 200f, 1f), Quaternion.Identity, 69f),
    };

    public static bool IsFishingActivity(int activityId) => ZonesByActivityId.ContainsKey(activityId);

    public static bool TryGet(int activityId, out FishingZoneConfig config) =>
        ZonesByActivityId.TryGetValue(activityId, out config);
}
