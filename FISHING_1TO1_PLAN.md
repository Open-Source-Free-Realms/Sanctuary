# Free Realms Fishing — Research + 1:1 Fidelity Plan

> Consolidated research on how real FR fishing worked, a gap matrix against our current
> server, and a phased plan to close the gaps. Companion to `FISHING_HANDOFF.md` (the map),
> `FISHING_RE_NOTES.md` (wire-level RE), and `FISHING_WIKI.md` (raw wiki data).
> Ground rule still applies: **the client binary + client assets are the source of truth**;
> the wiki fills in the data model but does NOT document rewards/XP/scoring numbers.

---

## Part A — What "1:1 Free Realms fishing" actually is

Fishing is the **Fisherman job** (`Profiles.json`: **Id 137, Type 21**, NameId 20178, Icon
20740, `MembersOnly:false`, `Trial:true`). A job in this engine is a *Profile* that carries a
persisted **Level + LevelXP** (`DbProfile`) and a client-facing XP bar (`AbilityExperience`:
Level / Progress / TotalForLevel). So fishing has the same leveling spine every other job has —
we just aren't feeding it yet.

The full 1:1 loop, from the wiki + binary + video:

1. **Unlock** the job (Sports Club Message Board quest in Sanctuary) → train with **Reed
   Stillwater** (Stillwater Crossing). Quests: *Becoming a Better Fisherman*, *Testing the
   Waters*, *Itty Bitty Bait* (Jonah Relicreel, Rainbow Lake).
2. **Equip a rod** (5 rods, each a cast-distance tier) and optionally a **lure** (13 lures,
   each +10% to 3 named fish; **Treasure Magnet** +10% treasure).
3. **Enter a fishing hole** (6 holes, all "Difficulty 1"): Sacred Grove Shallows, Rainbow Lake,
   Brambleback's Bayou, Darklit Lagoon, Wintery Basin, Frostbitten Banks.
4. **Aim + cast** — raycast to water, distance must be within the rod's Min/Max cast range;
   power = where in that range you land.
5. **Bite** — a fish gets interested, nibbles, then bites (server-driven).
6. **Reel** — bring the fish in. The real game has a **tension / reel-distance mechanic**
   (see the "reeling minigame" gap below): reel too hard and the line snaps, too slow and it
   escapes.
7. **Catch resolves** to one of: a **fish** (species gated by your fishing level; rolled with
   rarity weighting + lure bonus; each has a **size** Small/Medium/Large/Extra-Large that
   drives weight, held model scale, and the banner), **treasure** (chest → coins/loot/tools),
   or **junk** (boot / socks / dynamite gag).
8. **Rewards** — the caught item enters the bag, you gain **fishing job XP** (levels the job,
   unlocking higher fish), likely some **coin**, and the species is recorded in the relevant
   **fish Collection** ("What I've Caught"). Completing a fish collection awards job XP + a
   collection reward item.
9. Fish feed the **Chef** job as cooking ingredients (the `icon_activateable_fishing_*_bits/oil/
   soup` assets are cooked-fish consumables) — cross-job, out of scope for the core pass.

### Authoritative data model (all six holes)

Fish unlock at tiers **1 / 5 / 10 / 14 / 17 / 20**. Per-hole fish lists are already baked into
`FishingSession.ZoneFishTables` with real item/name/icon ids and verified against the wiki
(`FISHING_WIKI.md`). Fish → collection membership (from each hole's wiki "Collections"):

| Collection | Holes / fish source |
|---|---|
| Wilds Pond Fish + Wilds Stream Fish | Sacred Grove Shallows, Rainbow Lake |
| Briarwood Pond Fish + Briarwood Stream Fish | Brambleback's Bayou, Darklit Lagoon |
| Snowhill Pond Fish + Snowhill Stream Fish | Wintery Basin, Frostbitten Banks |
| Extra Large Catch of the Day | XL-size catches (Snowhill + Wilds holes) |
| Extra Large and in Charge | XL-size catches (Briarwood holes) |

**Pond vs Stream:** each hole lists a *Pond* and a *Stream* collection; the per-fish split
(which species is "pond" vs "stream") is **not on the wiki** — needs extraction from client
collection data (see Part D).

### The scoring fields (from the binary, `FishingResult` case 5)

The catch banner + FishScored UI consume: **FishName, IconId, Difficulty, Rarity, Size (1–4),
Score, TimeToCatch, Weight**. We currently fill these with *guessed* formulas. Real values come
from the client fish definitions (difficulty/rarity per species) — flagged in Part D.

---

## Part B — Current state (what already works)

From `FishingSession.cs` / `FISHING_HANDOFF.md`, verified end-to-end and multiplayer:

- ✅ Full cast→bite→fight→reel→catch loop, server-driven, correct camera/animation.
- ✅ Bobber, rod→line, underwater school, lunge/splash, reel-up, money shot.
- ✅ All 6 holes' real fish tables + Fish Finder (plain species names via Jenkins-hash string ids).
- ✅ Rarity-weighted roll (by min-level), size class drives weight + held model + banner.
- ✅ Treasure chest (static catchable head + gold burst) and junk (boot/socks/dynamite).
- ✅ **DB persistence of catches** (per-item immediate `DbItem` insert; bag survives relog).
- ✅ **Multiplayer sync** of fishing visuals to nearby players.

Infrastructure present but **not yet wired to fishing**: job Profiles with persisted Level/LevelXP,
the `AbilityExperience` XP-bar packet, the coin store's DB-write pattern, size-variant fish items
(the `68xxx` "Small/Medium/… <fish>" item ids already exist in `ClientItemDefinitions.json`).

---

## Part C — Gap matrix (current → 1:1)

| # | Gap | Fidelity impact | Have the pieces? |
|---|-----|-----------------|------------------|
| G1 | **Level gating** — fish rolled/greyed by fishing level | High (core progression) | ✅ Profile Level exists; min-levels in table |
| G2 | **Job XP on catch** — level the Fisherman job | High (core progression) | ✅ DbProfile.LevelXP + AbilityExperience; ❓ XP curve |
| G3 | **Catch-size variety** — roll Small/Med/Large/XL variant | Med (visible per catch) | ✅ size-variant item ids exist |
| G4 | **Lures** — +10% to 3 named fish + Treasure Magnet | Med | 🟢 **DONE (v1)** — activation via ability handler, +10% roll bonus, magnet→treasure. Open: lure duration model, item ids in finder (`FishLureRequirement`), whether client acks activation |
| G5 | **Rods → cast distance** — Min/Max per rod tier | Med (feel) | 🟢 **DONE (v1)** — equipped rod (slot 7) → 3 tiers → cast Min/Max. Open: real distance numbers (approximated; better rods only extend past baseline) |
| G6 | **Collections ("What I've Caught")** | High (headline missing feature) | ❌ from-scratch system (DB + packets + membership data) |
| G7 | **Reeling / tension minigame** — line snap, reel distance | High (the *skill* of fishing) | ⚠️ config defaults known; interaction model needs RE |
| G8 | **Coin rewards** on catch / treasure | Low–Med | ✅ coin-grant path exists; ❓ amounts |
| G9 | **Verify 5 holes' overworld spawns** (only 563 done) | Med (other holes untested) | ⚠️ per-zone Areas.xml + live test |
| G10 | **Scoring accuracy** — real difficulty/rarity/score/time | Low (cosmetic banner) | ❓ per-species values from client data |
| G11 | **Quests / trainer / unlock flow** | Low (meta, not minigame) | depends on quest system state |

---

## Part D — Data / RE that must be extracted first (unblocks the rest)

These are **research tasks**, grounded in the ground rule (binary + assets over guesses). Do these
before or alongside Phase 1; several gaps are blocked on them.

1. **Fishing XP curve (G2).** Find the per-level XP thresholds for a job and the per-catch XP
   award. Check client data for a profile/level table (`ClientProfileDefinitions` or a rank/XP
   curve); check the binary near the profile/rank code. If truly unavailable, adopt a documented
   default curve and per-fish XP = base × rarity, and mark it "approximated".
2. **Lure id map (G4).** Map each of the 13 lures + Treasure Magnet to (a) its equippable item id
   in `ClientItemDefinitions.json`, and (b) the `FishingLureRequirement` id the client expects in
   `ClientFishEntryInfo` (`FishingLureDataSource`). Confirm how the client reads the *equipped* lure
   (is the +10% purely server-side, or does the client's finder show it via the requirement field?).
3. **Rod → cast-distance tiers (G5).** Map the 5 rods to item ids and to Min/Max cast distances.
   Wiki gives 3 qualitative tiers ("short" / "greater" / "deepest"); real numbers should come from
   the rod item defs or `ServerFishingPlayerConfig` (defaults: Min 3.0 / Max 20.0).
4. **Collections system RE (G6).** The big one. Determine the client collection packet(s) and
   definition format: opcode/layout for the collection list + "collection item discovered" +
   "collection completed" events, and where `ClientCollectionDefinitions` live (client data). Map
   the 8 fish collections (Pond/Stream × 3 realms + 2 XL) to their member species and rewards.
   The Collections browser is a Scaleform UI — decompile it with FFDec like the fish finder was.
5. **Reeling/tension model (G7).** Decide whether real FR had an interactive tension meter or a
   scripted reel. Evidence *for* interactive: `FishingDistanceMeter:FishHooked`, reel-distance
   display models 1596–1619 (2fish/3fish at 3/6/9/12 m), and `ServerFishingPlayerConfig`
   (LineSnapChance, LineSnapTensionMinPercent, FishEscapeTensionMaxPercent, ReelSpeed/HookingReel/
   SuperReel speeds, TensionMeterSize). Trace `ControllerFishing` reel input + the distance-meter UI
   to see how much is client-simulated vs server-adjudicated.
6. **Per-species difficulty/rarity/size (G10) + coin amounts (G8).** From client fish/item defs.
7. **Verify holes 560/561/562/564/565 overworld spawns (G9).** From each `<zone>Areas.xml`.

---

## Part E — Phased implementation plan

Ordered by value-per-effort and dependency. Each phase is independently shippable and testable at
Sacred Grove (563), then swept across the other holes.

### Phase 1 — Progression core (G1 + G2)  ← highest value, pieces already exist
Make fishing *level up the job* and *gate fish by level*. This is the single biggest step toward
"feels like Free Realms."
- Read the player's Fisherman profile (Id 137) Level in `RollCatch` / `BuildFishFinderEntries`.
- **Gate the roll:** exclude fish whose `MinLevel > fishing level`; in the Fish Finder mark
  over-level fish `FishCatchable = false` (greyed) instead of dropping them.
- **Award XP on catch** (`Update` → Reeling, next to `GrantCaughtItem`): add LevelXP to the
  Fisherman profile, handle level-up (recompute Level from the curve), persist (DbProfile), and
  push the updated `AbilityExperience` bar to the client.
- Files: `FishingSession.cs`, `FishingSessionState.cs`, `Player.cs` (a `GrantJobXp(profileId, xp)`
  helper mirroring `GrantCaughtItem`), the profile/XP curve from Part D#1.
- Verify: catch fish → XP bar moves → cross a tier → a new fish appears catchable in the finder;
  relog → level/XP persisted.

### Phase 2 — Catch-size variety + accurate rewards (G3 + G8 + G10)
- Roll a **size** (Small/Med/Large/XL) per catch and grant the **size-variant item** (`68xxx`),
  not one representative id. Wire real per-species difficulty/rarity/score/time into the banner.
- Award **coins** on catch/treasure (amounts from Part D#6).
- XL catches are the trigger for the "Extra Large Catch of the Day" / "Extra Large and in Charge"
  collections — sets up Phase 4.
- Files: `FishingSession.cs` (RollCatch/RollMiscCatch, size→item map), `Player.cs` (coin grant).

### Phase 3 — Gear: rods + lures (G4 + G5)
- **Rods:** read the equipped rod → set `FishingPlayerConfig` Min/Max cast distance per tier.
- **Lures:** read the equipped lure → apply +10% to its 3 fish in the roll; Treasure Magnet → +10%
  treasure share (currently a flat `MiscCatchChance`). Populate `ClientFishEntryInfo.
  FishLureRequirement` so the finder reflects lure-boosted fish.
- Files: `FishingSession.cs`, the register handler (`FishingPacketRegisterPlayerRequestHandler.cs`),
  lure/rod maps from Part D#2/#3.

### Phase 4 — Collections / "What I've Caught" (G6)  ← headline missing feature, largest build
Depends on Part D#4 RE. Standalone subsystem:
- New DB table (SqLite + MySql migration) for per-character caught-species / collection progress.
- Server collection registry (the 8 fish collections + membership + rewards).
- On catch: record species (and XL for the XL collections); on collection completion award job XP +
  reward item.
- Send collection state on login + deltas on catch so the Collections browser renders "What I've
  Caught". (Mirror the `GrantCaughtItem` immediate-persist pattern.)
- Files: new `Sanctuary.Gateway/Collections/*`, DB entities + migration, login + fishing hooks.
- **Scope check:** this touches core systems (DB, login) and needs its own 1-client test pass;
  keep it on its own branch/commit series.

### Phase 5 — Reeling / tension minigame (G7)  ← deepest fidelity, do last / optional
Depends on Part D#5. If real FR was interactive, implement the tension/reel-distance loop using the
`ServerFishingPlayerConfig` defaults (reel speeds, snap/escape tension, distance meter, super-reel),
driving `FishingDistanceMeter` + the 1596–1619 distance models. This converts "reel = auto-catch
after fight" into the actual skill mechanic. High risk, high polish — gate on confirming it existed.

### Phase 6 — Sweep + polish (G9 + G11)
- Verify/fix the other 5 holes' overworld spawn positions; confirm activity→hole mapping (only 563
  is verified). Junk/treasure per-hole tuning. Optionally the unlock quest/trainer flow (G11) if the
  quest system supports it.

---

## Part F — Suggested order & rationale

**P1 → P2 → P3 → P4 → (P5) → P6.** P1 delivers the most "it's really Free Realms now" per unit
effort and reuses existing infra. P2/P3 are medium, self-contained polish. P4 is the headline
feature but a big standalone build (do it deliberately, with RE first). P5 is the deepest fidelity
and the riskiest — only commit once we've confirmed the interactive reel actually existed. P6 is
cleanup that can trail throughout.

**Blocking research (Part D) to schedule up front:** XP curve (P1), lure/rod maps (P3), collections
packet RE (P4), reel-model RE (P5). D#1–#3 are quick asset/def lookups; D#4–#5 are real IDA/FFDec RE
sessions.
