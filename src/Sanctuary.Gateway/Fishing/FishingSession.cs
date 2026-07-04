using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Fishing;

/// <summary>
/// Server-side fishing state machine for a single player.
///
/// The Free Realms client is a "terminal" for fishing: it animates the bobber and fish
/// autonomously but the *bite* is driven entirely by the server via
/// <see cref="FishingPacketUpdateProxiedFishingBobber"/> (sub-opcode 10). See FISHING_RE_NOTES.md.
///
/// Flow (all S->C guids are the PLAYER guid — the client resolves the proxied fishing player by it):
///   RegisterPlayerRequest -> RegisterPlayerResponse (+ UpdateData + FishInfoUpdate)
///   CastRequest           -> SpawnProxiedFishingBobber + SpawnFishRun (school of ambient fish)
///   (timer)               -> UpdateProxiedFishingBobber(Flag2=true)  = biter interested / swims in
///   (timer)               -> UpdateProxiedFishingBobber(Flag1=true)  = biter bites (lunge + fight)
///   ReelInRequest         -> FishingResult(caught)      | (timeout) escape + FishingResult(nothing)
/// </summary>
public sealed class FishingSession
{
    // Underwater-minigame fish models (client preloads these in FishingProcessor::SetInFishing).
    public const int ThinFishModelId = 1670;
    public const int MediumFishModelId = 1671;
    public const int FatFishModelId = 1672;

    // Bobber model (fishing_bobber_bbe.adr). The SpawnProxiedFishingBobber "Unknown" field is the
    // bobber model id: the client only constructs the bobber object when it is > 0 (sub_CCFDB0).
    // With 0 the bobber is never created and the subsequent SpawnFishRun null-derefs it -> crash.
    public const int BobberModelId = 1063;

    // sg_fishing_lure_bbe (models.txt 1673, "in-world fishing lure") — the lure on the line/water.
    public const int LureModelId = 1673;

    // Held "catch" models shown in the money shot (sg_fish_catch_*_bbe), by size. The fishing minigame
    // has NO per-species fish meshes — species identity is the name + icon; the 3D model is one of these
    // generic "held up your catch" shapes chosen by size (matches the client's own fishing models).
    public const int CatchFishSmallModelId = 1623;
    public const int CatchFishMediumModelId = 1621;
    public const int CatchFishLargeModelId = 1620;
    public const int CatchFishXLargeModelId = 1622;

    // sg_fishing_treasure_chest_bbe (models.txt 1624) — the reeled-up treasure chest in the money shot.
    public const int TreasureChestModelId = 1624;

    // Money-shot composite effects (ActorCompositeEffectDefinitions.xml). The fish sparkle plays when you
    // hold up a caught fish; the chest burst is the treasure chest popping open with a gold sparkle.
    public const int MoneyShotFishEffectId = 15880;  // BBE_PFX_fishing_moneyshot_fish
    public const int MoneyShotChestEffectId = 15881; // BBE_PFX_fishing_moneyshot_chest (bursts the chest open)

    // Models sent in the RegisterPlayerResponse model-id list. NOTE: the client CreateActors each id
    // (sub_B68600 -> m_ActorIds) at the WORLD ORIGIN and never repositions them (m_ActorIds is only
    // touched by the ctor/dtor) — so every id here becomes a stray actor parked at (0,0,0). We keep it
    // to the underwater fish only (matches the original), so the origin isn't cluttered and a bobber
    // appearing at (0,0,0) can only be the real cast bobber (a placement bug), not a preload artifact.
    public static readonly int[] PreloadModelIds =
    {
        ThinFishModelId, MediumFishModelId, FatFishModelId,
    };

    // The client's SpawnFishRun handler ALWAYS spawns one actor at the hook from the packet's Unknown2
    // model (the "decoy" that self-despawns on bite) — there is no zero-fish path, and Unknown2=0
    // null-derefs. Point that decoy at model 69 (widget_01.adr, "Invisible Block") so the forced hook
    // actor renders nothing. It is registered (so no crash) but has no visible mesh. See FISHING_RE_NOTES.md.
    private const int InvisibleDecoyModelId = 69;

    // The underwater fish-arena world coords the client HARDCODES (only X is configurable, via
    // FishingZoneConfig.Unknown3 = the zone's Underwater_Bed X). Confirmed as flt_18227B0 / dword_18227B4.
    private const float UnderwaterBedY = -8f;
    private const float UnderwaterBedZ = 485f;
    // The bobber floats at the bed's water surface. The client parks the surface lure ~10 above the
    // arena floor (dword_18227AC), so the surface sits at Y = -8 + 10 = +2. The bobber MUST be here
    // (not at the overworld cast spot ~400 units away) or it renders outside the fishing camera.
    private const float BobberSurfaceY = UnderwaterBedY + 10f;

    // FishingResult.ResultType values (client OnRoutePacket case 14 inner switch).
    private const int ResultNothingCaught = 0;
    private const int ResultScoredCatch = 5;

    // The ambient fish (Unknown7=false) we drive with interested/hooked so it visibly swims to the
    // rod, LUNGES and bites, fights, then gets reeled up. Only ambient fish animate those states.
    // UpdateProxiedFishingBobber(FishIndex=Biter) also sets the client's "current fish" so the
    // subsequent reel (ResultType 4) pulls up THIS fish.
    private const int BiterFishId = 2;

    // Time from hooking to the ambient fish finishing its lunge+fight and dropping into the reelable
    // nibble state (client hardcodes ~1.7s). Reel (ResultType 4) only pulls the fish up once there.
    private const int FightDurationMs = 1900;

    private enum Phase
    {
        Idle,
        BobberOut,   // bobber + fish spawned, ambient fish wandering
        Nibbling,    // biter is interested and swimming to the hook
        Hooked,      // biter has bitten and is fighting/on the line; player may reel
        ReelPending, // player reeled; waiting for the fight to finish before the reel-up
        Reeling      // reel-up animation playing, catch banner pending
    }

    // FishingResult ResultType values (client OnRoutePacket case 14 inner switch).
    private const int ResultReelDrag = 4;  // fish dragged toward the player (reel-in animation)

    private readonly object _gate = new();
    private readonly Player _player;

    private Phase _phase = Phase.Idle;

    private ulong _bobberGuid;
    private long _nextEventAtMs;
    private long _hookedAtMs;
    private long _lastCastAtMs = long.MinValue;

    // The lure the player has active (item-def id; 0 = none). A lure gives +10% catch chance to its
    // three named fish; the Treasure Magnet raises the treasure share instead. Set when the player
    // activates a lure consumable (AbilityPacketClientRequestStartAbility) and cleared when they leave.
    private int _activeLureItemId;

    // Selected catch for the active cast.
    private int _fishModelId = ThinFishModelId;  // UNDERWATER biter model (thin/medium/fat) by fish shape
    private int _catchModelId = CatchFishSmallModelId; // HELD money-shot model (catch fish by size, or chest)
    private bool _staticOnLine;                  // misc catch (treasure/junk): sits on the hook, no bite
    private int _lineModelId = ThinFishModelId;  // the object parked on the hook for a static catch
    private bool _isItemCatch;                   // treasure/junk -> FishingResult.Caught=true (item, not fish)
    private int _moneyShotEffectId = MoneyShotFishEffectId; // composite effect burst in the money shot
    private int _fishNameId;          // fish-name string-table id (@32) — localized caught message
    private int _fishIconId;
    private int _fishSize = 1;       // 1..4, size selector (small/medium/large/xlarge) + hand scale
    private int _fishDifficulty = 1;
    private int _fishRarity = 1;
    private int _fishScore = 10;
    private float _fishWeight = 1f;  // shown in the caught banner as "%2.2f"
    private string _fishName = "Trout";

    public int ActivityId { get; private set; }
    public FishingZoneConfig ZoneConfig { get; private set; }

    public FishingSession(Player player)
    {
        _player = player;
    }

    /// <summary>Records which fishing activity/zone the player entered (from the minigame-start handler).</summary>
    public void SetZone(int activityId, FishingZoneConfig zoneConfig)
    {
        lock (_gate)
        {
            ActivityId = activityId;
            ZoneConfig = zoneConfig;
        }
    }

    private static long NowMs => Environment.TickCount64;

    /// <summary>Handles a CastRequest: spawn the bobber and the catchable fish, begin the bite timeline.</summary>
    public void OnCast(Vector4 targetPosition)
    {
        lock (_gate)
        {
            // The client's CastRequest is delivered several times per cast; ignore the duplicates so
            // we don't spawn a stack of bobbers/fish. Guard the sentinel: on the very first cast
            // `_lastCastAtMs` is long.MinValue and `castNow - long.MinValue` overflows to a negative
            // value (< 500), which would drop EVERY cast and leave the player stuck casting.
            var castNow = NowMs;
            if (_lastCastAtMs != long.MinValue && castNow - _lastCastAtMs < 500)
                return;
            _lastCastAtMs = castNow;

            _bobberGuid = _player.Guid;

            // Place the bobber at the overworld cast spot (on the pond, where the player is looking).
            var bobberPosition = targetPosition;
            bobberPosition.W = 1f;

            // The bobber's second Vector4 is NOT a quaternion — the client (case 8 + sub_770A70) drops
            // it into matrix row 2 as the FORWARD direction and rebuilds the basis from it via cross
            // products. A zero direction (0,0,0,1) collapses the whole 3x3 to zero => the bobber is
            // scaled to nothing (placed correctly but invisible). Send a real forward direction (+Z).
            SendProxied(new FishingPacketSpawnProxiedFishingBobber
            {
                Guid = _player.Guid,
                Unknown = BobberModelId, // bobber model id — must be > 0 or the client never creates the bobber
                Position = bobberPosition,
                Rotation = new Vector4(0f, 0f, 1f, 0f) // forward = +Z (must be non-zero or the bobber is zero-scaled)
            });

            // Choose the catch for this cast.
            RollCatch();

            // The client (case 18 of FishingProcessor::OnRoutePacket) ALWAYS renders one object at the
            // hook. If the head fish is CATCHABLE (Unknown7=true) it parks at the hook, FROZEN in the
            // nibble state, and stays the "current fish" the reel-up pulls — reeling is permitted anytime.
            // If the head is NOT catchable, the client spawns a self-removing decoy from Unknown2 and we
            // drive a separate ambient biter for the swim-in + lunge. See FISHING_RE_NOTES.md.
            var ambientModels = new[] { ThinFishModelId, MediumFishModelId, FatFishModelId };

            if (_staticOnLine)
            {
                // Treasure / junk: the item is ALREADY on the line as a static catchable head — no swim,
                // no bite; the player just left-clicks (reels) to bring it up (matches the real game).
                var fish = new List<UnderwaterFishSpawnInfo> { CatchableFish(1, _lineModelId) };
                for (var i = 0; i < 3; i++)
                    fish.Add(AmbientFish(11 + i, ambientModels[i % ambientModels.Length])); // scenery wanderers

                // SELF ONLY: the client's SpawnFishRun handler (case 18) derefs the bobber with no null
                // check, so a peer that hasn't created our bobber (e.g. it can't resolve our proxied
                // character) CRASHES on it. The underwater fish are our private sim anyway — peers only
                // need the bobber + poses + result, which all null-check safely.
                _player.SendTunneled(new FishingPacketSpawnFishRun
                {
                    Unknown = true,
                    Unknown2 = InvisibleDecoyModelId,
                    Unknown3 = string.Empty,
                    UnderwaterFishSpawns = fish
                });

                // Ready to reel immediately; it waits on the hook until reeled (no auto-escape).
                _phase = Phase.Hooked;
                _hookedAtMs = NowMs;
                _nextEventAtMs = long.MaxValue;

                FishingSessions.Logger?.LogInformation(
                    "Fishing[{guid}] cast -> {name} is on the line (static, model {model}); reel to bring it up",
                    _player.Guid, _fishName, _lineModelId);
                return;
            }

            var biterFish = new List<UnderwaterFishSpawnInfo>
            {
                AmbientFish(10, ambientModels[0]),      // head wanderer — keeps the decoy self-removing on bite
                AmbientFish(BiterFishId, _fishModelId)  // the biter that swims in and lunges (matches the catch)
            };
            for (var i = 0; i < 4; i++)
                biterFish.Add(AmbientFish(11 + i, ambientModels[i % ambientModels.Length]));

            // SELF ONLY (see the static-catch path above): SpawnFishRun null-derefs the bobber on a peer
            // that hasn't created it, so keep the underwater fish private.
            _player.SendTunneled(new FishingPacketSpawnFishRun
            {
                Unknown = true,
                Unknown2 = InvisibleDecoyModelId, // hide the forced hook decoy (invisible-block model)
                Unknown3 = string.Empty,
                UnderwaterFishSpawns = biterFish
            });

            _phase = Phase.BobberOut;
            // Wait for the rod->bobber line to appear before engaging the fish. The client only builds
            // that line once OUR proxied fishing player reaches its "bobber out & fishing" pose (state 4),
            // which its local FishingProcessor does ~2s AFTER this bobber spawn (it recreates the bobber
            // into a line-ready form there — see FISHING_RE_NOTES.md "group-7 gate"). Engaging the fish
            // sooner made the bite land on top of that ~2s mark, so the line only ever showed up "at the
            // bite". Hold interest to ~3.5-5s post-spawn so the line settles visibly first (and it reads
            // like real FR, where the bobber floats a beat before a fish notices it).
            _nextEventAtMs = NowMs + Random.Shared.Next(3500, 5000);

            FishingSessions.Logger?.LogInformation(
                "Fishing[{guid}] cast -> spawned bobber (model {bobber}) + biter fish {fish} (model {model}) at {pos}; interest in ~{ms}ms",
                _player.Guid, BobberModelId, BiterFishId, _fishModelId, bobberPosition, _nextEventAtMs - NowMs);
        }
    }

    /// <summary>Handles a ReelInRequest: resolve the cast to a catch or a miss.</summary>
    public void OnReel()
    {
        lock (_gate)
        {
            var now = NowMs;

            if (_phase == Phase.Hooked)
            {
                // Reeled once the fish has bitten -> a catch. The reel-up (ResultType 4) only pulls the
                // fish up after its lunge+fight finishes (~FightDurationMs after the bite); if the
                // player reels sooner, wait out the fight before the reel-up so it actually animates.
                var reelReadyAt = _hookedAtMs + FightDurationMs;
                _phase = Phase.ReelPending;
                _nextEventAtMs = reelReadyAt > now ? reelReadyAt : now;

                FishingSessions.Logger?.LogInformation(
                    "Fishing[{guid}] reel-in after bite -> reel-up in ~{ms}ms then catch {name} (size {size}, weight {weight})",
                    _player.Guid, _nextEventAtMs - now, _fishName, _fishSize, _fishWeight);
            }
            else if (_phase is Phase.BobberOut or Phase.Nibbling)
            {
                // Reeled BEFORE the bite -> the fish spooks and runs off; no catch.
                FishingSessions.Logger?.LogInformation(
                    "Fishing[{guid}] reel-in before the bite ({phase}) -> fish runs off, nothing caught",
                    _player.Guid, _phase);
                SendUpdateBobber(hooked: false, interested: false); // escape -> the interested fish flees
                SendNothingCaught();
                _phase = Phase.Idle;
            }
            else
            {
                // ReelPending / Reeling (already resolving) or Idle: ignore.
                FishingSessions.Logger?.LogInformation(
                    "Fishing[{guid}] reel-in while {phase} -> ignored", _player.Guid, _phase);
            }
        }
    }

    /// <summary>Drives the bite timeline. Call periodically (server tick).</summary>
    public void Update()
    {
        lock (_gate)
        {
            if (_phase == Phase.Idle)
                return;

            var now = NowMs;
            if (now < _nextEventAtMs)
                return;

            switch (_phase)
            {
                case Phase.BobberOut:
                    // The biter becomes interested and swims to the hook.
                    SendUpdateBobber(hooked: false, interested: true);
                    _phase = Phase.Nibbling;
                    _nextEventAtMs = now + Random.Shared.Next(1200, 1800); // time to reach the hook
                    FishingSessions.Logger?.LogInformation(
                        "Fishing[{guid}] fish INTERESTED (swimming to hook); bite in ~{ms}ms", _player.Guid, _nextEventAtMs - now);
                    break;

                case Phase.Nibbling:
                    // The biter reaches the hook and bites (lunge + splash), then fights.
                    SendUpdateBobber(hooked: true, interested: false);
                    _phase = Phase.Hooked;
                    _hookedAtMs = now;
                    // Give the player a window to reel before the fish escapes.
                    _nextEventAtMs = now + 12000;
                    FishingSessions.Logger?.LogInformation(
                        "Fishing[{guid}] fish BIT (lunge + fight); reel now! escapes in 12s", _player.Guid);
                    break;

                case Phase.Hooked:
                    // Player was too slow — the fish escapes.
                    FishingSessions.Logger?.LogInformation("Fishing[{guid}] fish ESCAPED (not reeled in time)", _player.Guid);
                    SendUpdateBobber(hooked: false, interested: false);
                    SendNothingCaught();
                    _phase = Phase.Idle;
                    break;

                case Phase.ReelPending:
                    // Fight finished (fish is in the reelable nibble state) — send the reel-up.
                    FishingSessions.Logger?.LogInformation("Fishing[{guid}] reel-up (fish pulled to the rod)", _player.Guid);
                    SendReelDrag();
                    _phase = Phase.Reeling;
                    _nextEventAtMs = now + 1800; // let the reel-up play before the catch show-off
                    break;

                case Phase.Reeling:
                    // Reel-up finished. Grant the fish to the inventory first (the "item added"
                    // popup fires right as the reel ends), then show the catch banner (name + size).
                    var granted = FishingSessions.GrantCaughtItem(_player, _fishItemId);
                    FishingSessions.Logger?.LogInformation(
                        "Fishing[{guid}] caught {name} (item {item}, granted {granted}); showing catch banner (size {size}, weight {weight})",
                        _player.Guid, _fishName, _fishItemId, granted, _fishSize, _fishWeight);
                    SendCatchResult();
                    _phase = Phase.Idle;
                    break;
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _phase = Phase.Idle;
            _activeLureItemId = 0; // a fresh hole session starts with no lure active
        }
    }

    // A catchable fish. Size (1..4) = small/medium/large/xlarge (the catch "size word", the held-catch
    // model, and the weight). Shape (1..3) = thin/medium/fat, the UNDERWATER biter mesh (the fishing
    // minigame has no per-species meshes, so a shark reads as thin/long, a grouper/globfish as fat).
    // NameId is the PLAIN species-name string id (NOT a size-prefixed item name): the client resolves it
    // as "Global.Text.<NameId>" (Jenkins lookup2 hash into en_us_data.dat). ItemId is a representative
    // catch item (granted to the bag); IconId is the fish icon. MinLevel is the wiki unlock tier (weights
    // rarity). Ids from ClientItemDefinitions.json / FISHING_WIKI.md (freerealms.fandom.com Category:Fishing).
    private readonly record struct FishEntry(string Name, int ItemId, int NameId, int IconId, int Size, int MinLevel, int Shape);

    // Sacred Grove Shallows (activity 563).
    private static readonly FishEntry[] SacredGroveFish =
    [
        new("Slugmud Skipper", 68049, 99924, 4276, 1, 1, 1),
        new("Tickled Trout", 68001, 99963, 4422, 1, 1, 2),
        new("Flutterfish", 2868, 23278, 3737, 1, 1, 2),
        new("Butter Flyfish", 68053, 99925, 4254, 2, 5, 3),
        new("Cheery Salmon", 68063, 99927, 4257, 2, 10, 2),
        new("Chipsen Fish", 68067, 99928, 4258, 3, 14, 2),
        new("Feral Catfish", 68056, 99926, 4262, 3, 17, 3),
        new("Baconfish", 68069, 99929, 4249, 4, 20, 3),
        new("Tangletooth Shark", 68113, 407895, 4421, 4, 20, 1),
    ];

    // Rainbow Lake (activity 562).
    private static readonly FishEntry[] RainbowLakeFish =
    [
        new("Flutterfish", 2868, 23278, 3737, 1, 1, 2),
        new("Calico Catfish", 2149, 6171, 4595, 1, 1, 3),
        new("Tickled Trout", 68001, 99963, 4422, 1, 1, 2),
        new("Toothy Tetra", 68066, 99923, 4281, 2, 5, 1),
        new("Peachy Perch", 68051, 99920, 4341, 2, 10, 2),
        new("Finless Fish", 68046, 99919, 4263, 3, 14, 1),
        new("Lady Tetra", 68061, 99922, 4270, 3, 17, 1),
        new("Tangletooth Shark", 68113, 407895, 4421, 4, 20, 1),
    ];

    // Brambleback's Bayou (activity 561).
    private static readonly FishEntry[] BramblebacksBayouFish =
    [
        new("Creeping Cod", 68081, 99930, 4260, 1, 1, 2),
        new("Bitter Betta", 68097, 99935, 4250, 1, 1, 2),
        new("Globfish", 2147, 6169, 4593, 1, 1, 3),
        new("Blind Swurglefish", 2865, 23275, 195, 2, 5, 1),
        new("Changed Salmon", 68094, 99940, 4256, 2, 10, 2),
        new("Fanged Grouper", 68091, 99939, 4261, 3, 14, 3),
        new("Briar Nibbler", 68101, 99941, 4556, 3, 17, 3),
    ];

    // Darklit Lagoon (activity 560).
    private static readonly FishEntry[] DarklitLagoonFish =
    [
        new("Ink Cod", 68073, 99931, 4635, 1, 1, 2),
        new("Creeping Cod", 68081, 99930, 4260, 1, 1, 2),
        new("Golden Scaled Nettler", 68009, 99971, 4420, 1, 1, 2),
        new("Thorny Trout", 68090, 99934, 4280, 2, 5, 2),
        new("Old Sole", 68087, 99933, 4271, 2, 10, 2),
        new("Purplenosed Shark", 68075, 99932, 4274, 3, 14, 1),
        new("Roach Loach", 68099, 99936, 4275, 3, 17, 1),
    ];

    // Wintery Basin (activity 564).
    private static readonly FishEntry[] WinteryBasinFish =
    [
        new("Frozen Char", 68027, 99945, 4266, 1, 1, 2),
        new("Winter Piranha", 68022, 99942, 4282, 1, 1, 2),
        new("Frostgill Smelt", 2864, 23274, 196, 2, 5, 1),
        new("Spineless Stickleback", 68039, 99947, 4277, 2, 10, 1),
        new("Blubracuda", 68036, 99946, 4252, 3, 14, 3),
        new("Coach Loach", 68024, 99944, 4259, 3, 17, 1),
        new("Ferocious Fangler", 68008, 99970, 4419, 4, 20, 3),
    ];

    // Frostbitten Banks (activity 565).
    private static readonly FishEntry[] FrostbittenBanksFish =
    [
        new("Chilly Grouper", 68011, 99948, 4267, 1, 1, 3),
        new("Winter Piranha", 68022, 99942, 4282, 1, 1, 2),
        new("Frostgill Smelt", 2864, 23274, 196, 2, 5, 1),
        new("Pacu Pacu", 68028, 99951, 4272, 2, 5, 2),
        new("Blue Thornfin", 68014, 99949, 4253, 2, 10, 1),
        new("Spineless Stickleback", 68039, 99947, 4277, 2, 10, 1),
        new("Goofy Grouper", 68030, 99952, 4269, 3, 14, 3),
        new("Talking Bass", 68019, 99950, 4279, 3, 17, 2),
        new("Ferocious Fangler", 68008, 99970, 4419, 4, 20, 3),
        new("Plattypus", 68033, 99953, 4273, 4, 20, 3),
    ];

    // Per-activity fish tables, keyed by fishing-hole activity id (all six holes from the wiki).
    private static readonly Dictionary<int, FishEntry[]> ZoneFishTables = new()
    {
        [563] = SacredGroveFish,
        [562] = RainbowLakeFish,
        [561] = BramblebacksBayouFish,
        [560] = DarklitLagoonFish,
        [564] = WinteryBasinFish,
        [565] = FrostbittenBanksFish,
    };

    private FishEntry[] CurrentFishTable =>
        ZoneFishTables.TryGetValue(ActivityId, out var table) ? table : SacredGroveFish;

    // A non-fish catch (treasure/junk) — "reel in everything from exotic fish to treasure and coin!"
    // CatchModel is the held money-shot model. Fish gear/junk has no dedicated held mesh, so junk reuses
    // the same generic held model the underwater biter used (a fish), so the money shot matches what was
    // on the line, with the name + icon identifying the real item. CatchModel = 0 means "use that biter
    // model"; a real model id (e.g. the treasure chest) overrides it.
    private readonly record struct MiscCatch(string Name, int ItemId, int NameId, int IconId, int Size, int CatchModel, float Weight);

    private static readonly MiscCatch[] MiscCatches =
    [
        new("Treasure Chest", 3016, 5091, 6347, 3, TreasureChestModelId, 5f),
        new("Old Boot", 68103, 99954, 4430, 1, 0, 3f),
        new("Soggy Socks", 68104, 99955, 4435, 1, 0, 3f),
        new("Soggy Stick of Dynamite", 68002, 99964, 4436, 1, 0, 2f),
    ];

    // Fraction of casts that pull up a misc catch (treasure/junk) instead of a fish. TODO(lure): the
    // Treasure Magnet lure should raise the treasure share once equipped lures are read (FISHING_WIKI.md).
    private const float MiscCatchChance = 0.15f;

    private static int BiterModelForShape(int shape) => shape switch
    {
        1 => ThinFishModelId,
        2 => MediumFishModelId,
        _ => FatFishModelId,
    };

    private int _fishItemId;

    // ----- Gear: rods (cast distance) + lures (+10% catch chance) ------------------------------------
    // Rod item-def id ranges from ClientItemDefinitions.json: 5 rods, each with 35 tint variants
    // (68185-68359) plus one high id (76687-76691). The wiki groups them into three cast tiers:
    // Bamboo/Golden Reel "a short distance", Metal/Red Scoped "a greater distance", Golden Scoped "the
    // deepest of waters". We map each to a Min/Max cast distance sent in FishingPlayerConfig (the client
    // validates a cast only when the water-raycast distance is within [Min, Max]). NOTE: better rods only
    // EXTEND the range past the tested baseline (Max 20) — never shorten it — so the known-good Sacred
    // Grove flow can't regress. Absolute numbers are approximate pending real cast data (FISHING_1TO1_PLAN.md D#3).
    private readonly record struct RodTier(string Name, float MinCast, float MaxCast);

    private static RodTier RodTierForDefinition(int rodDef)
    {
        if ((rodDef is >= 68185 and <= 68219) || rodDef == 76687) return new("Simple Bamboo Fishing Rod", 3f, 20f);
        if ((rodDef is >= 68220 and <= 68254) || rodDef == 76688) return new("Golden Reel Fishing Rod", 3f, 20f);
        if ((rodDef is >= 68255 and <= 68289) || rodDef == 76689) return new("Metal Fishing Rod", 3f, 25f);
        if ((rodDef is >= 68290 and <= 68324) || rodDef == 76690) return new("Red Scoped Fishing Rod", 3f, 25f);
        if ((rodDef is >= 68325 and <= 68359) || rodDef == 76691) return new("Golden Scoped Fishing Rod", 3f, 32f);
        return new("(no rod)", 3f, 20f); // no/unknown rod -> the tested default cast range
    }

    /// <summary>The rod equipped in the active (Fisherman) profile's weapon slot (slot 7); 0 if none.</summary>
    private int EquippedRodDefinitionId()
    {
        var profile = _player.Profiles.FirstOrDefault(x => x.Id == _player.ActiveProfileId);
        if (profile is not null && profile.Items.TryGetValue(7, out var profileItem))
        {
            var clientItem = _player.Items.FirstOrDefault(x => x.Id == profileItem.Id);
            if (clientItem is not null)
                return clientItem.Definition;
        }
        return 0;
    }

    /// <summary>Min/max cast distance for the player's equipped rod (sent in FishingPlayerConfig).</summary>
    public (float Min, float Max) GetCastDistance()
    {
        var tier = RodTierForDefinition(EquippedRodDefinitionId());
        FishingSessions.Logger?.LogInformation(
            "Fishing[{guid}] rod = {rod} -> cast distance {min}..{max}", _player.Guid, tier.Name, tier.MinCast, tier.MaxCast);
        return (tier.MinCast, tier.MaxCast);
    }

    // Lure item-def id (68152-68164) -> the three fish it gives +10% catch chance (wiki). The names MUST
    // match the FishEntry.Name strings in the zone tables. Treasure Magnet (68164) boosts treasure and is
    // handled separately (EffectiveMiscCatchChance + RollMiscCatch's chest bias).
    private const int TreasureMagnetItemId = 68164;

    private static readonly Dictionary<int, string[]> LureFishBonus = new()
    {
        [68152] = ["Winter Piranha", "Toothy Tetra", "Blind Swurglefish"],          // 16oz Steak
        [68153] = ["Chilly Grouper", "Butter Flyfish", "Briar Nibbler"],            // Flyfisher 3000
        [68154] = ["Blubracuda", "Baconfish", "Changed Salmon"],                    // French Fry
        [68155] = ["Frostgill Smelt", "Flutterfish", "Ink Cod"],                    // Frostflies
        [68156] = ["Goofy Grouper", "Finless Fish", "Fanged Grouper"],              // Mega Slider
        [68157] = ["Plattypus", "Lady Tetra", "Roach Loach"],                       // Nightcrawlers
        [68158] = ["Pacu Pacu", "Peachy Perch", "Globfish"],                        // Perch Pinpointer
        [68159] = ["Coach Loach", "Feral Catfish", "Creeping Cod"],                 // Shiny Crankbait
        [68160] = ["Frozen Char", "Slugmud Skipper", "Old Sole"],                   // Skipper Seeker
        [68161] = ["Talking Bass", "Cheery Salmon", "Bitter Betta"],                // Sleek Clicker
        [68162] = ["Blue Thornfin", "Chipsen Fish", "Thorny Trout"],                // Thorn Jig
        [68163] = ["Spineless Stickleback", "Calico Catfish", "Purplenosed Shark"], // Tiny Rib
    };

    // Maps the client's ability request id to a lure item-def id (68152-68164). We accept EITHER the
    // ActivatableAbilityId (4287-4299, aligned/consecutive with the item ids) OR the item def id itself,
    // because it isn't yet confirmed which the client sends in AbilityPacketClientRequestStartAbility.
    public static int LureItemIdForAbility(int abilityOrItemId) =>
        abilityOrItemId is >= 4287 and <= 4299 ? 68152 + (abilityOrItemId - 4287)
        : abilityOrItemId is >= 68152 and <= 68164 ? abilityOrItemId
        : 0;

    /// <summary>Records the lure the player just activated. Persists until they leave the hole (Reset).</summary>
    public void SetActiveLure(int lureItemId)
    {
        lock (_gate)
        {
            _activeLureItemId = lureItemId;
            FishingSessions.Logger?.LogInformation("Fishing[{guid}] active lure = item {lure}", _player.Guid, lureItemId);
        }
    }

    // The base misc (treasure/junk) share of casts; the Treasure Magnet lure raises it by +10%.
    private float EffectiveMiscCatchChance() =>
        _activeLureItemId == TreasureMagnetItemId ? MiscCatchChance + 0.10f : MiscCatchChance;

    private void RollCatch()
    {
        // A fraction of casts surface treasure or junk instead of a fish (raised by the Treasure Magnet).
        if (Random.Shared.NextDouble() < EffectiveMiscCatchChance())
        {
            RollMiscCatch();
            return;
        }

        // Roll among the zone's fish, weighting rarer (higher-tier) fish down so low-level fish dominate.
        // TODO(fish-table): gate by the player's fishing level (FISHING_1TO1_PLAN.md P1).
        var table = CurrentFishTable;

        // The active lure (if any) gives its three named fish a +10% catch chance.
        var lureFish = _activeLureItemId != 0 && LureFishBonus.TryGetValue(_activeLureItemId, out var lf) ? lf : null;

        var weights = new float[table.Length];
        var total = 0f;
        for (var i = 0; i < table.Length; i++)
        {
            weights[i] = 6f / (table[i].MinLevel + 5f); // ~1.0 at level 1, tapering to ~0.24 at level 20
            total += weights[i];
        }

        // Apply the lure bonus: add +10% of the base total to each matching fish (an ~absolute +10% chance).
        if (lureFish is not null)
        {
            var boost = 0.10f * total;
            for (var i = 0; i < table.Length; i++)
            {
                if (Array.IndexOf(lureFish, table[i].Name) >= 0)
                {
                    weights[i] += boost;
                    total += boost;
                }
            }
        }

        var roll = (float)Random.Shared.NextDouble() * total;
        var idx = 0;
        for (; idx < table.Length - 1; idx++)
        {
            if (roll < weights[idx]) break;
            roll -= weights[idx];
        }

        var f = table[idx];
        _fishName = f.Name;
        _fishItemId = f.ItemId;                    // inventory item definition id (granted on catch)
        _fishNameId = f.NameId;                    // @32 localized name id
        _fishIconId = f.IconId;                    // @36 icon id
        _fishSize = f.Size;
        _fishModelId = BiterModelForShape(f.Shape);// underwater biter mesh (thin/medium/fat) by body shape
        _catchModelId = _fishModelId;              // held money-shot mesh = the same underwater fish model
        _staticOnLine = false;                     // a real fish swims in and bites
        _isItemCatch = false;                      // a real fish (Caught=false -> size + weight banner)
        _moneyShotEffectId = MoneyShotFishEffectId; // fish sparkle on the hold-up
        _fishDifficulty = 1;                       // all holes are "Difficulty 1" per the wiki
        _fishRarity = Math.Clamp(f.MinLevel / 5 + 1, 1, 5);
        _fishScore = 10 * _fishSize;
        _fishWeight = _fishSize * 1.5f + (float)Random.Shared.NextDouble() * _fishSize * 2f;
    }

    // With the Treasure Magnet active, the treasure chest is strongly favored within the misc pool.
    private float MiscWeight(in MiscCatch m) =>
        _activeLureItemId == TreasureMagnetItemId && m.Name == "Treasure Chest" ? m.Weight * 4f : m.Weight;

    private void RollMiscCatch()
    {
        var total = 0f;
        foreach (var m in MiscCatches) total += MiscWeight(m);

        var roll = (float)Random.Shared.NextDouble() * total;
        var pick = MiscCatches[0];
        foreach (var m in MiscCatches)
        {
            var w = MiscWeight(m);
            if (roll < w) { pick = m; break; }
            roll -= w;
        }

        _fishName = pick.Name;
        _fishItemId = pick.ItemId;
        _fishNameId = pick.NameId;
        _fishIconId = pick.IconId;
        _fishSize = pick.Size;
        // Catches WITH their own model (the treasure chest) sit STATIC on the hook and are reeled
        // straight up — the chest is visible on the line, no swim/bite. Junk (boot/socks/dynamite) has no
        // model, so it rides the normal fish-bite path and is revealed as a generic fish with the item's
        // name/icon (a "gotcha" — you get a bite, reel up, and it's an Old Boot).
        _staticOnLine = pick.CatchModel > 0;
        _catchModelId = _staticOnLine ? pick.CatchModel : MediumFishModelId;
        _fishModelId = MediumFishModelId;   // a generic fish is on the line (the chest overrides via _lineModelId)
        _lineModelId = _catchModelId;       // what's parked on the hook for a static catch
        _isItemCatch = true;                // treasure/junk -> item money shot (Caught=true), not a fish
        // The treasure chest bursts open with its own effect; junk (shown as a fish) uses the fish sparkle.
        _moneyShotEffectId = _staticOnLine ? MoneyShotChestEffectId : MoneyShotFishEffectId;
        _fishDifficulty = 1;
        _fishRarity = 1;
        _fishScore = 5 * _fishSize;
        _fishWeight = pick.Size * 1.5f + (float)Random.Shared.NextDouble() * pick.Size;
    }

    /// <summary>
    /// Builds the "Fish Finder" list (the fishing UI panel of catchable fish for this hole) from the
    /// current zone's fish table, so it matches what <see cref="RollCatch"/> can actually roll.
    /// Sent in FishingPacketFishInfoUpdate. NOTE: the client (FishingProcessor::sub_B65ED0) dedupes
    /// entries by <see cref="ClientFishEntryInfo.Type"/> and skips any with Unknown4=true, so each fish
    /// needs a UNIQUE Type — reusing the three underwater models (thin/medium/fat) would collapse the
    /// list to three rows. We key Type on the item-def id (unique + stable); the name/icon come straight
    /// from NameId/IconId.
    /// </summary>
    public List<ClientFishEntryInfo> BuildFishFinderEntries()
    {
        var table = CurrentFishTable;
        var entries = new List<ClientFishEntryInfo>(table.Length);
        foreach (var f in table)
        {
            // NOTE: "What I've Caught" is NOT driven by these entries — it's the fish COLLECTIONS system
            // (CollectionsBrowser), a separate per-player tracked+persisted feature we don't implement yet.
            // FishSpecial/FishCatchable here only affect the Fish Finder's Fish tab.
            entries.Add(new ClientFishEntryInfo
            {
                Type = f.ItemId,        // unique dedup key per fish
                NameId = f.NameId,      // localized fish name shown in the finder
                IconId = f.IconId,      // fish icon shown in the finder
                FishSpecial = false,
                FishCatchable = true,   // TODO(fish-table): false when the fish is above the player's level
                FishLureRequirement = 0 // TODO(lure): map the wiki lure -> FishingLureDataSource id (FISHING_WIKI.md)
            });
        }

        return entries;
    }

    /// <summary>An ambient (decorative) fish that actively wanders the school; not catchable.</summary>
    private static UnderwaterFishSpawnInfo AmbientFish(int id, int modelId) => new()
    {
        Unknown = id,
        ModelId = modelId,
        TintAlias = string.Empty,
        TextureAlias = string.Empty,
        Unknown5 = 1,
        Unknown6 = 0,
        Unknown7 = false, // ambient
        Unknown8 = 1.5f,
        Unknown9 = 2.0f,
        Unknown10 = 1.5f,
        Unknown11 = 1.0f,
        Unknown12 = 1.0f,
        Unknown13 = 2.5f,
        Unknown14 = 1.5f,  // wander speed — keep them moving so none look stationary
        Unknown15 = 0.5f,
        Unknown16 = 2.5f,
        Unknown17 = 0.3f,  // short idle between moves
        Unknown18 = 1.2f
    };

    /// <summary>
    /// The catchable HEAD object, parked on the hook and FROZEN (client sub_CD3A70 starts a catchable
    /// fish in the nibble state 6, and state 6 skips the wander for a catchable fish — so it never
    /// swims/lunges). Used for static catches like a treasure chest that sit on the line ready to reel.
    /// Being the head + catchable also makes it the "current fish" the reel-up pulls, and suppresses the
    /// forced hook decoy. The movement params are still needed: the reel-up GLIDE (state 7, sub_CD1A10)
    /// moves the object by speed = param/param, so zeroing them makes it teleport instead of gliding up.
    /// See FISHING_RE_NOTES.md.
    /// </summary>
    private static UnderwaterFishSpawnInfo CatchableFish(int id, int modelId) => new()
    {
        Unknown = id,
        ModelId = modelId,
        TintAlias = string.Empty,
        TextureAlias = string.Empty,
        Unknown5 = 1,
        Unknown6 = 0,
        Unknown7 = true, // catchable -> parked at the hook, frozen (no swim/bite); reeled straight up
        Unknown8 = 1.5f,
        Unknown9 = 2.0f,
        Unknown10 = 1.5f,
        Unknown11 = 1.0f,
        Unknown12 = 1.0f,
        Unknown13 = 2.5f,
        Unknown14 = 1.5f,
        Unknown15 = 0.5f,
        Unknown16 = 2.5f,
        Unknown17 = 0.3f,
        Unknown18 = 1.2f
    };

    /// <summary>
    /// Sends a fishing VISUAL packet to nearby players AND ourselves. All fishing S->C guids are our
    /// player guid, so each visible player's client renders us as a "proxied fishing player" (bobber,
    /// cast/reel poses, bite, catch animation) at our world spot — multiplayer sync. A FishingResult
    /// keyed to our guid animates our proxied character on their screen without diving THEIR camera
    /// (that money shot is only for a player's own catch). Inventory/reward grants stay private (they go
    /// straight to us via GiveItem), so only the visuals are shared.
    /// </summary>
    private void SendProxied(ISerializablePacket packet) =>
        _player.SendTunneledToVisible(packet, sendToSelf: true);

    private void SendUpdateBobber(bool hooked, bool interested)
    {
        SendProxied(new FishingPacketUpdateProxiedFishingBobber
        {
            Guid = _player.Guid,
            Unknown = BiterFishId, // drive the ambient biter (the catchable head fish ignores flags),
                                   // which also makes it the "current fish" the reel-up pulls in
            Flag1 = hooked,
            Flag2 = interested
        });
    }

    /// <summary>ResultType 4: plays the reel-in drag animation (fish dragged to the player, line attached).</summary>
    private void SendReelDrag()
    {
        SendProxied(new FishingPacketFishingResult
        {
            Guid = _player.Guid,
            ResultType = ResultReelDrag,
            Caught = _isItemCatch, // treasure/junk are items, not fish (no size scaling on the reel-up)
            FishId = _fishNameId,
            FishName = string.Empty,
            Unknown8 = _catchModelId, // held-catch model (>0) — sg_fish_catch by size, or the chest
            Unknown12 = _fishSize
        });
    }

    /// <summary>
    /// ResultType 5: the catch — plays the show-off ("money shot"), fires the caught banner
    /// (name + size word + weight), and auto-returns the player to the normal camera.
    /// Field offsets verified in FISHING_RE_NOTES.md; the banner needs @80 (held-fish model) > 0.
    /// NOTE: the client does NOT grant the item — inventory must be updated by a separate packet.
    /// </summary>
    private void SendCatchResult()
    {
        SendProxied(new FishingPacketFishingResult
        {
            Guid = _player.Guid,
            ResultType = ResultScoredCatch,
            // @28 Caught: false = a fish (money shot scaled by size + shows size word & weight); true = an
            // ITEM like the treasure chest (fixed hold-up scale, name-only banner — no fish size/weight).
            Caught = _isItemCatch,
            FishId = _fishNameId,      // @32 fish-name string-table id (localized)
            Unknown1 = _fishIconId,    // @36 scoring / numeric param
            FishName = string.Empty,   // @40 unused by the banner
            Unknown2 = _fishWeight,    // @56 WEIGHT ("%2.2f")
            Unknown3 = _fishSize,      // @60 size selector 1..4 (small/medium/large/xlarge)
            Unknown4 = _fishWeight,    // @64 scoring weight/score
            Unknown5 = _fishDifficulty,// @68 scoring
            Unknown6 = _fishRarity,    // @72 scoring
            Unknown7 = _fishScore,     // @76 scoring
            Unknown8 = _catchModelId,  // @80 show-off held MODEL (>0 or no banner) — catch fish / chest
            UnknownStr1 = string.Empty,// @84 held-fish tint
            UnknownStr2 = string.Empty,// @100 held-fish texture
            Unknown9 = _moneyShotEffectId, // @116 money-shot composite effect (fish sparkle / chest burst)
            Unknown10 = 0,
            Unknown11 = false,
            Unknown12 = _fishSize      // @120 show-off size class 1..4
        });
    }

    private void SendNothingCaught()
    {
        SendProxied(new FishingPacketFishingResult
        {
            Guid = _player.Guid,
            ResultType = ResultNothingCaught,
            Caught = false,
            FishId = 0,
            FishName = string.Empty
        });
    }
}
