# Fishing Mini-Game — Handoff / Onboarding

> **Read this first.** This is the entry point for a fresh session. It covers *what works
> now*, *where everything lives*, and *how to keep going*. The deep reverse-engineering log
> (packet offsets, IDA addresses, client function names) is in **`FISHING_RE_NOTES.md`** —
> refer to it when you need the wire-level details; this file is the map.

---

## 1. Goal & ground rules

**Goal:** Reverse engineer the Free Realms fishing mini-game and get it working end-to-end
on the emulated **Sanctuary** game server.

**Ground rules the user set (important):**
- The **game binary (IDA Pro MCP) and packet logs are the absolute reference.** Do *not*
  treat the pre-existing server fishing code as authoritative — verify against the client.
- Reference gameplay video: https://www.youtube.com/watch?v=lM7Pzhp9h6k
- This work runs under `/loop` (self-paced). Commit after each meaningful change.

**Status: fishing works end-to-end.** Cast → bobber + underwater fish school → fish gets
interested & swims in → bites (lunge + splash) → fights → reel → reel-up animation →
inventory grant + yellow "item received" text → catch banner (name / size / weight) →
auto-return to normal camera. Reel-timing gate is implemented (early reel = miss).

### Latest state (read this — supersedes stale details below)
- **Bobber: FIXED & visible.** It was invisible because we sent the bobber's second Vector4 as
  `(0,0,0,1)`. That field is a **forward DIRECTION, not a quaternion**; a zero direction made the
  client's `sub_770A70` collapse the bobber's matrix to zero-scale (placed correctly, rendered at
  size 0). Now sends `(0,0,1,0)`. See FISHING_RE_NOTES.md "BREAKTHROUGH".
- **Rod→bobber line: FIXED (timing).** The line (`sub_CD0150`) builds only once BOTH the bobber actor
  (with its `LINE` socket) exists AND our proxied char has attachment **group 7** (the rod, read for its
  `EMITTER2` socket). Group 7 rides the fishing-pose bit — set from the cast (`CastAnimRequest`→state 2,
  relayed to self) — so the rod is attached early. The *line* itself, though, is only (re)built when our
  local `FishingProcessor` reaches its **state 4** ("bobber out & fishing"), which it does ~2s AFTER the
  server's bobber spawn (`Process` case 3 → `sub_CCFFB0(player,4)` recreates the bobber into a line-ready
  form). The old bite timeline made the fish bite at ~spawn+3s, right on top of that ~spawn+2s mark, so
  the line only ever *seemed* to appear "at the bite". **Fix: delay interest to ~3.5-5s post-spawn** (in
  `OnCast`) so the line settles visibly before the fish engages. `IsCurrentPlayer` is always true for us
  (`m_pProxiedCharacter == this`), so the bobber-spawn packet takes the no-op state-3 path and we depend
  on that local state-4 transition — do NOT preload the bobber model (it would make the line build on the
  case-8 bobber, which state 4 then destroys → orphaned line).
- **Docs:** `FISHING_WIKI.md` = full wiki data (all 6 fishing holes + per-zone fish/level tables +
  lure→fish map). `FISHING_RE_NOTES.md` = wire-level RE. This file = the map.
- **Debug:** type `tporigin` (no slash) in chat to TP to the world origin; `tp x y z` for coords.
- **FishTable + Fish Finder: DONE for all six holes.** `FishingSession.ZoneFishTables` (keyed by
  activity id 560-565) holds every hole's real wiki fish; `RollCatch` rolls them (rarity-weighted by
  min-level) and `BuildFishFinderEntries()` feeds the Fish Finder (`FishInfoUpdate`). The client dedupes
  finder entries by `Type` and skips `Unknown4=true`, so each fish gets a unique `Type` (its item id).
  **Names are the PLAIN species name, not size-prefixed** (`NameId` = the species-name string id, e.g.
  Slugmud Skipper = 99924, resolved by the client as `"Global.Text.<NameId>"`). The string-id space is
  a **Jenkins lookup2 hash** of `"Global.Text.<id>"` into `en_us_data.dat` (the `ucdt` table) — see
  [[fishing-key-re-facts]] for how the plain-name ids were recovered. Caveat: only 563 (Sacred Grove)
  has a verified overworld spawn + confirmed hole identity; the other activity→hole assignments are
  best-effort from the `bw_/sh_` pond/stream zone-name hints in `FishingActivityZones`. Still TODO:
  gate the Fish Finder / roll by the player's fishing level, and the +10% lure bonus.
- **Models + misc catches: DONE.** The minigame has NO per-species fish meshes — species = name/icon; the
  3D model is a generic shape. Each fish now carries a **Shape** (thin/medium/fat → underwater biter mesh
  1670-1672, so sharks read thin/long, groupers/globfish fat). The catch banner's held model (`@80`) uses
  that **same underwater fish model** — the money-shot `sg_fish_catch_*` meshes (1620-1623) rendered wrong,
  so `@80` was reverted to the underwater model (the known-good behavior). **Misc catches** (`MiscCatches`,
  ~15% of casts): Treasure Chest (money-shot model 1624 = `sg_fishing_treasure_chest_bbe`), Old Boot, Soggy
  Socks, Soggy Stick of Dynamite.
  - **Treasure Chest** has a real model (1624), so it uses the client's static **catchable-head** mechanic:
    it's spawned as a `Unknown7=true` head fish (frozen at the hook — `CatchableFish()`), so the chest is
    already ON THE LINE when the underwater cam arrives and the player just reels it up (no swim/bite). The
    session goes straight to `Hooked` on cast and waits (no auto-escape). Being the catchable head keeps it
    the "current fish" the reel-up (RT4) pulls, and suppresses the decoy.
  - **Junk** (boot/socks/dynamite) has no model, so it rides the normal fish-bite path and is revealed as a
    generic fish carrying the item's name/icon (a "gotcha").
  TODO: Treasure Magnet lure should raise the treasure share; chests could grant loot on open.

### Session log — persistence, multiplayer, treasure polish (latest; all committed on `fishing-minigame`)
Chronological git trail (newest last): `9f16553` revert held money-shot to underwater model · `0de82fd`
junk shows a fish not a chest in the money shot · `6e8bb0f` treasure chest is a static catchable-head on
the line · `c2ec600` money-shot item mode (`Caught=true`) + burst effects · `846a086` fix chest teleport
on reel-up (restore glide params) · `0caad26` persist catches to DB · `1956829` relay fishing visuals to
peers · `7905e01` stop `SpawnFishRun` crashing peers · `8ac567e` fix one-way player visibility · `369d615`
fix DB persistence (per-character item id) · `c9d0642`/`e5ef926` "What I've Caught" experiment + revert.

- **DB persistence of catches: DONE & VERIFIED.** Catches now persist in the bag across relog.
  `FishingSessions.GrantCaughtItem` (in `FishingSessionState.cs`) inserts a `DbItem` immediately (crash-
  safe, like the coin store) then grants in-memory with `GiveItem(def,count,id)`. **Gotcha:** `DbItem`'s
  key is the composite `(Id, CharacterId)` and is NOT auto-generated — you must assign the id yourself
  (`player.Items` max+1); leaving it 0 hits `UNIQUE constraint failed: Items.Id, Items.CharacterId` and it
  silently falls back to in-memory. `SavePlayerToDatabase` still does NOT reconcile Items on logout — this
  per-catch immediate write is the pattern. `GiveItem` gained an explicit-id overload for this.
- **Money-shot item mode + effects: DONE.** `FishingResult.Caught` (@28) is the "item vs fish" flag: false
  = fish (size-scaled hold-up + size/weight banner), true = ITEM (fixed scale, name-only banner). Set true
  for treasure/junk (`_isItemCatch`). The money-shot composite effect is `@116 Unknown9` (adjacency to the
  @120 size class confirms it): fish → `BBE_PFX_fishing_moneyshot_fish` (15880), chest →
  `BBE_PFX_fishing_moneyshot_chest` (15881, the gold burst). Effect ids from `ActorCompositeEffectDefinitions.xml`.
- **Treasure chest on the line + glide: DONE.** Spawned as a frozen catchable head (`CatchableFish()`,
  `Unknown7=true`, model 1624) so it sits ON the hook (no swim/bite); phase goes straight to `Hooked`,
  `_nextEventAtMs=long.MaxValue` (waits, no escape). **Gotcha:** the reel-up GLIDE (client `sub_CD1A10`
  state 7) moves the object by speed = param/param from the `UnderwaterFishSpawnInfo` movement fields, so
  `CatchableFish` must keep the normal (ambient) movement params — zeroing them made the chest TELEPORT up.
- **Multiplayer sync: WORKING (user-confirmed).** Fishing keeps you in the shared zone, all fishing guids
  are your player guid, and the client's `sub_B64300` is find-or-create — so relaying the fishing VISUALS
  to visible players makes their client spawn a proxied fishing player and render you fishing. Route them
  via `SendProxied` (SendTunneledToVisible + self). **Do NOT relay:** `SpawnFishRun` (its handler null-
  derefs the bobber on a peer that hasn't created it → CRASH; keep the underwater sim self-only — peers only
  need the null-safe bobber/pose/bite/result); and `RegisterPlayerResponse`/`FishInfoUpdate`/`UpdateData`
  (case 3 writes the GLOBAL FishingProcessor config + the current player's camera → would corrupt peers'
  own fishing). Inventory/reward grants stay private.
- **Player visibility: FIXED (concurrent-join race).** Was intermittent one-way ("I see them, they don't
  see me"). `UpdateEntityZoneTile` (BaseZone.cs) cross-notified nearby players BEFORE adding the entity to
  its own tile, so two players loading at once each finished scanning before the other had added itself.
  Fix: add the entity to its tile FIRST, then cross-notify. (Core-server fix, not fishing-specific.)
- **"What I've Caught": NOT persisted — needs the COLLECTIONS system.** It's fishing ability/tab 3 (abilities
  are Fish Finder / Tackle Box / What I've Caught). It fills client-side from catch events and resets on
  relog; there is NO fishing packet or fish-finder flag that restores it (verified — decompiled the
  Scaleform `.gfx` UIs with FFDec at `C:\Program Files (x86)\FFDec\ffdec-cli.exe`, and every fishing
  sub-opcode is accounted for). Persisting it requires implementing FR **collections** (per-player
  caught-species tracking + a new DB table/migration for SqLite+MySql + collection packets on login). The
  BAG is already the durable catch record; only the collections *browser view* is missing. See
  [[fishing-key-re-facts]].

### Session log — ambient fishing scenery: wandering fish + spot markers [WIP, movement unconfirmed]
**Goal (user):** fill the water of every fishing hole with fish that swim around like the underwater
minigame fish, plus the light-blue "whirlpool" markers that show where you can fish. Visible in the
water, not just during fish-cam.

**Status:** partial. Server-spawned fish **render** (user confirmed seeing a school), but **movement is
not yet confirmed** and they currently spawn at a **diagnostic location**, not the real ponds. Commits:
`842ae0e` (initial) + the follow-up commit this entry documents.

**Confirmed this session:**
- Server-spawned NPCs DO render for the player (user saw the fish). The entity/visibility/AddNpc path works.
- Models: **418** `fish.adr` (the fish), **1684** `sg_fishing_node_01` (the fishing-spot whirlpool marker).
  Other candidates found: 1064 fish_bbe, 1670-1672 underwater fish, 1727 sg_fishing_run_school_bbe,
  1704/1705 sg_fishing_area_bubbles/particle, 1685 node_02.

**Architecture found (important):**
- The server has ONE zone: **FabledRealms** (`StartingZone`, Id 1), player spawn `(-1904.883, -39.7098,
  412.6024)` (from `src/Resources/Zones/FabledRealms.json`).
- **Each fishing hole is a SEPARATE CLIENT SCENE**, not a server zone. `MiniGameStartGamePacketHandler`
  sends `PacketClientBeginZoning` with `Name = fishingZone.ZoneName` (`sg_fishing_medpond`,
  `sg_fishing_stream`, `bw_fishing_medpond`, `bw_fishing_stream`, `sh_fishing_medpond`,
  `sh_fishing_stream`), `GeometryId 214`, `Position = fishingZone.SpawnPosition`. The client loads that
  scene; the server does NOT create a zone object for it. So a fish spawned in FabledRealms may or may not
  render once the client has zoned into a fishing scene — **unverified**, and a likely reason the first
  placement at `(435,-64,370)` showed nothing at the hole (player's server position/scene didn't match).
- **Real water positions live in the user's Unity project** (ForgeLightToolkit import of the FR client):
  `C:\Users\bobya\Documents\FLTKSample\Assets\ForgeLight\FreeRealms\*.gzne` — `FabledRealms.gzne` + the six
  fishing-hole zone files above. `.gzne` = zone header/eco/flora/invisible-walls (parser: the toolkit's
  `GzneFile.cs`); the actual placed-object instances (model + position) are in the per-chunk `.gcnk` files
  (`Gcnk/RuntimeObject.cs`). This is the data source for exact per-hole pond coords (not yet mined).

**Implementation (files):**
- `src/Sanctuary.Game/Entities/AmbientFishNpc.cs` (new): a fish NPC that wanders within a radius of a home
  point on the water plane, driven by the zone's 10 Hz tick. Broadcasts its position each tick via
  `PlayerUpdatePacketUpdatePosition` (opcode 125) — the same packet that relays player movement.
- `src/Sanctuary.Game/Zones/BaseZone.cs`: protected `NextEntityGuid()` + `TryRegisterNpc(Npc)` so a zone
  can spawn custom-subclass NPCs (guid space starts at 100 billion — no collision with player guids).
- `src/Sanctuary.Game/Zones/StartingZone.cs`: `SpawnFishingScenery()` (called from the ctor) spawns the
  fish + a spot-marker NPC. **Currently spawns at the zone login point `(-1904.883, -39.7098, 412.6024)`
  as a DIAGNOSTIC** (`SpawnPosition`), NOT the real ponds. `tp -1904.883 -39.7098 412.6024` to reach them.

**Movement debugging (the open problem):**
- v1 broadcast to the fish's `VisiblePlayers` → fish rendered but were **static**. Likely cause: a fish
  only learns about a player (populating its `VisiblePlayers`) if that player was flagged `Visible` during
  the tile scan, which isn't guaranteed — so the movement updates were sent to nobody.
- v2 (this commit) broadcasts to **`Zone.Players`** (all players in the zone) + sets the opcode-125
  `State` byte to `1` (moving) instead of `0` (idle/teleport). **UNTESTED.**
- If still static after v2: opcode 125 may not drive NPC actors on the client at all → next step is to
  verify against the client binary (IDA: find the opcode-125 / position-update handler and whether it
  resolves NPC actors) and, if needed, find/add the correct NPC-movement packet.

**Next steps (in order):**
1. Confirm v2 makes the fish swim (`tp` to the diagnostic spot above). If not → IDA the movement path.
2. Mine real per-hole water coords from the `.gzne`/`.gcnk` files (FabledRealms + the 6 fishing scenes).
3. Verify whether FabledRealms server-NPCs render *inside* a fishing scene after `BeginZoning`; if not,
   spawn the ambient fish via the fishing session / a fishing packet instead (the channel the underwater
   fish already use), or create server-side fishing-scene zones.
4. Relocate `SpawnFishingScenery` from the diagnostic login point to a data-driven per-hole placement.

### Session log — gear: rods (cast distance) + lures (+10% / Treasure Magnet) [builds clean; live-test pending]
Phase 3 of `FISHING_1TO1_PLAN.md` (G4+G5). Files: `FishingSession.cs`, `FishingSessionState.cs`,
`FishingPacketRegisterPlayerRequestHandler.cs`, `AbilityPacketClientRequestStartAbilityHandler.cs`.
- **Rods → cast distance.** The equipped rod (Fisherman profile weapon **slot 7** → `ClientItem.Definition`)
  maps by id range to 3 cast tiers, sent as `FishingPlayerConfig` Min/Max. Rod id ranges (verified from
  `ClientItemDefinitions.json`, each rod = 35 tint variants + 1 high id): Bamboo 68185-68219/76687 &
  Golden Reel 68220-68254/76688 = "short" (3..20, = tested baseline); Metal 68255-68289/76689 & Red
  Scoped 68290-68324/76690 = "greater" (3..25); Golden Scoped 68325-68359/76691 = "deepest" (3..32).
  **Better rods only EXTEND past the tested Max 20 — never shorten it — so the known-good 563 cast can't
  regress.** Absolute numbers are approximate (no real cast data yet — plan D#3).
- **Lures → +10% / Treasure Magnet.** Lures are `Class 142` SingleUse consumables, item ids **68152-68164**,
  `ActivatableAbilityId` **4287-4299** (both consecutive/aligned). Activating a lure comes in as
  `AbilityPacketClientRequestStartAbility` (`Data.Id` = the ability id) — the handler (which used to reject
  everything with "can't use that") now, if the ability maps to a lure AND the player has a fishing session,
  records the active lure, **consumes one** from the bag (`FishingSessions.ConsumeItem` → `ItemUpdate`/
  `ItemDelete` + DB), and returns without the failure. `RollCatch` gives the lure's 3 named fish +10% of the
  base weight total; **Treasure Magnet** (68164) instead raises the misc share +10% and 4× the chest weight.
  Active lure persists for the hole session (cleared on `Reset`).
- **Open / not 1:1 yet:** real per-rod cast distances + per-fish lure-requirement ids in the Fish Finder
  (`ClientFishEntryInfo.FishLureRequirement` left 0); lure duration model (currently "until you leave");
  and whether the client needs an ability-activation ack (only `AbilityPacketFailed` exists S→C — we send
  nothing on success, the bag update is the feedback). Verify these live.
- **Live-test checklist:** equip each rod tier → confirm you can cast further with Metal/Golden Scoped;
  activate a lure at a hole that has one of its fish (e.g. Perch Pinpointer at Bayou → Globfish) → confirm
  the lure count drops in the bag and that fish shows up more; Treasure Magnet → more chests.

### Next up (agreed with user)
1. **Collections system** for "What I've Caught" (the above) — a scoped standalone effort, needs 1-client testing.
2. Fishing polish still open: **level gating** (grey/roll by fishing level — min-levels already in the table),
   **rods → cast distance** (`FishingPlayerConfig` Min/Max), **lures** (+10% + Treasure Magnet; needs the
   FishingLureDataSource id map), **catch-size variety** (roll Small/Med/Large/XL variant items).
3. Verify the other 5 holes' real overworld spawns + confirm activity→hole mapping (only 563 verified).

---

## 2. Environment & paths

| What | Where |
|------|-------|
| Workspace root | `c:\Users\bobya\FRController\Sanctuary-minigame - backup - fish spawning over water` |
| Server source | `src/` (C# .NET) |
| Git branch | `fishing-minigame` (this repo is a working copy; nested repos are gitignored) |
| IDA target | Free Realms client via **ida-pro-mcp** MCP server (decompile/disasm/xrefs/py_eval) |
| Dumped client assets | `C:\Users\bobya\Documents\Free Realms Unpacker\editz fr assets\FR Assets 2025-07-07` |
| Live client folder | `C:\Users\bobya\AppData\Local\OSFRLauncher\Servers\EDITz's Local Server\Client` |
| Client logs | `…\EDITz's Local Server\Client\Logs` |
| Gateway console log | `src/bin/Debug/Logs/Sanctuary.Gateway-Console-<date>.log` |
| Packet log dumps | `…\packet logs\` (no fishing packets in them — do not expect any) |

---

## 3. Build & test loop

**Build (from `src/`):**
```
dotnet build Sanctuary.Gateway/Sanctuary.Gateway.csproj -c Debug
```
> A running Gateway **locks the DLL** — if the build fails with a file-lock error, the
> server is still running. Stop it, rebuild, restart.

**Test:**
1. Build, then start the Gateway server.
2. In the client, go to a fishing spot and fish. **Sacred Grove (activity 563)** is the
   one with a real overworld spawn position — use it for testing.
3. Watch `src/bin/Debug/Logs/Sanctuary.Gateway-Console-<date>.log` — the `FishingSession`
   logger narrates the whole bite timeline (cast → interested → bit → reel → catch).
4. For client-side crashes/errors, check `…\Client\Logs`.

---

## 4. The architecture in one screen

The client is a **terminal**: it animates the bobber and fish autonomously, but the **bite
is 100% server-driven**. The server-side fishing simulation does **not** exist in the client
binary (its config loaders are dead code). We drive everything from the C# server.

**Server projects:**
- `Sanctuary.Gateway` — UDP packet handlers + the fishing session state machine (our code).
- `Sanctuary.Game` — `Player` and game entities (inventory `GiveItem` lives here).
- `Sanctuary.Packet` / `Sanctuary.Packet.Common` — packet definitions / shared structs.
- `Sanctuary.Core.IO` — `PacketWriter` / `PacketReader`.

**Tick:** the Gateway main loop (`GatewayService.cs`) calls `Fishing.FishingSessions.Tick()`
every iteration, which calls `Update()` on each active session — that's what advances the
bite timeline on timers.

### 4.1 Packet flow (opcode 138 = 0x8A, header = int16 opcode + int16 sub-opcode, LE)

```
C→S RegisterPlayerRequest   S→C RegisterPlayerResponse (+ UpdateData + FishInfoUpdate)
C→S CastRequest             S→C SpawnProxiedFishingBobber + SpawnFishRun (school)
                            S→C (timer) UpdateProxiedFishingBobber Flag2=true  = interested (swims in)
                            S→C (timer) UpdateProxiedFishingBobber Flag1=true  = bite (lunge + fight)
C→S ReelInRequest           S→C FishingResult ResultType 4 (reel-up) then 5 (catch)
                              …or, reeled too early → escape + ResultType 0 (nothing)
```

All S→C guids are the **PLAYER guid** — the client resolves the proxied fishing player by it.

---

## 5. Key files & their roles

**State machine (the heart):**
- `src/Sanctuary.Gateway/Fishing/FishingSession.cs` — per-player state machine. Read this
  first; it's heavily commented. Phases: `Idle → BobberOut → Nibbling → Hooked →
  ReelPending → Reeling`. Contains the `FishTable`, timings, and all S→C packet builders.
- `src/Sanctuary.Gateway/Fishing/FishingSessionState.cs` — `FishingSessions` static registry
  (ConcurrentDictionary keyed by player guid), the static `Logger`, `GetOrCreate/Remove`,
  and `Tick()`.
- `src/Sanctuary.Gateway/Fishing/FishingActivityZones.cs` — per-activity zone config incl.
  the critical **`UnderwaterBedX`** (see §6.2). Values read from each zone's
  `<zone>Areas.xml` "Underwater_Bed" area.

**Handlers (C→S):**
- `src/Sanctuary.Gateway/Handlers/BaseFishingPacketHandler.cs` — dispatch. **Passes
  `reader.Span` (full payload incl. header)**, not `RemainingSpan`; every fishing
  `TryDeserialize` re-validates the 138+subop header. (This was a real bug — casts silently
  failed to deserialize when the header was stripped.)
- `…/Handlers/BaseFishingPacket/FishingPacketRegisterPlayerRequestHandler.cs` — sends the
  config packets (player config, zone config w/ `UnderwaterBedX`, fish model ids, fish info).
  **Deliberately does NOT send `SpawnProxiedFishingSchool`** (see §6.3).
- `…/BaseFishingPacket/FishingPacketCastRequestHandler.cs` → `session.OnCast(position)`.
- `…/BaseFishingPacket/FishingPacketReelInRequestHandler.cs` → `session.OnReel()`.
- `…/Handlers/BaseMiniGamePacket/MiniGameStartGamePacketHandler.cs` — creates the session,
  calls `SetZone(stateId, fishingZone)` + `Reset()`.

**Packets / structs:**
- `src/Sanctuary.Packet/BaseFishingPacket/FishingPacketFishingResult.cs` — the catch/result
  packet. `Unknown2`/`Unknown4` are **floats** (weight). Field-offset map in the file & notes.
- `src/Sanctuary.Packet.Common/Fishing/UnderwaterFishSpawnInfo.cs` — fish spawn struct.
  `Unknown8..17` are **floats** (movement params). *Int values froze the fish* — `int 1`
  reinterpreted as float ≈ 1.4e-45 ≈ 0.
- `src/Sanctuary.Packet.Common/Fishing/FishingPlayerConfig.cs` — `Unknown2`/`Unknown3` are
  **floats** (min/max cast distance).
- `src/Sanctuary.Packet.Common/Fishing/FishingZoneConfig.cs` — `Unknown3`/`Unknown6` **floats**.
- `src/Sanctuary.Packet/RewardBundlePacketSingleItem.cs` — **opcode 50, sub-type 2**: the
  yellow "You received: X" bottom-screen text. **Display-only** (does not grant the item).
- `src/Sanctuary.Game/Entities/Player.cs` — `GiveItem(int definitionId, int count=1)`:
  creates a `ClientItem`, adds to `Items`, sends `ClientUpdatePacketItemAdd` (bag) +
  `RewardBundlePacketSingleItem` (yellow text). **In-memory only — not persisted to DB.**

**Wiring:**
- `src/Sanctuary.Gateway/GatewayService.cs` — main loop calls `FishingSessions.Tick()`.
- `src/Sanctuary.Gateway/GatewayConnection.cs` — `OnTerminated` calls `FishingSessions.Remove()`.

---

## 6. Critical RE findings (the "why", condensed)

These are the non-obvious things that cost real debugging time. Full detail in
`FISHING_RE_NOTES.md`.

### 6.1 The bite is server-driven; ambient vs. catchable fish
`UpdateProxiedFishingBobber` (sub-opcode 10) with `Flag2` = interested, `Flag1` = bite is
what makes a fish swim in, **lunge (anim 103/104 + splash CE 16265)**, and fight. **Only
*ambient* fish (`Unknown7=false`) animate these states** — the "catchable" fish is frozen by
design. So we spawn an **ambient "biter"** fish (`BiterFishId = 2`) and drive *it*. Driving
the catchable fish produced a motionless fish that ignored the flags (an early bug). Setting
the biter as the update target also makes it the client's "current fish", so the reel-up
(ResultType 4) pulls *that* fish.

### 6.2 "Fish in the sky" — the Underwater_Bed positioning
The client **hardcodes the underwater fish arena at world Y=-8, Z=485**; only **X** is
configurable (via `FishingZoneConfig.Unknown3`). Sending the *overworld pond X* put the fish
above the wrong water (they appeared "in the sky"). Fix: send the zone's **`Underwater_Bed`
area X** (found in each `<zone>Areas.xml` in the dumped assets). Per-activity X values live in
`FishingActivityZones.cs` (e.g. Sacred Grove `sg_fishing_medpond` = 563 → X≈68).

### 6.3 "Stacked fish in the center" — don't send SpawnProxiedFishingSchool
The client's `SpawnProxiedFishingSchool` path (`sub_CD12A0`) places **every** fish in the
school at the **same point** → a clump of fish stacked on one spot. We removed those packets
entirely. The lively school comes from **`SpawnFishRun`** (sent on cast) with per-fish
positions/movement instead. (The user twice reported stacked fish; this was the cause.
Note: they *do* want wandering fish — just not stacked ones.)

### 6.4 Catch banner requirements
`FishingResult` for the catch (ResultType 5): the banner only shows if **@80 (held-fish
model) > 0** and **@32 (fish-name id) is a real string-table id**. Placeholders gave
"STRING 0 NOT FOUND". We use real fish (see FishTable below). `Caught=false` (@28) makes it
show *size word + weight*.

### 6.5 Reel timing gate
- Reel while `Hooked` (fish has bitten, hasn't fled) → **catch** (reel-up waits out the
  ~1.9s fight so it animates, then the catch banner).
- Reel while `BobberOut`/`Nibbling` (before the bite) → **fish spooks & runs off + "Nothing
  Caught"**.
- Reel while `ReelPending`/`Reeling`/`Idle` → ignored.

### 6.6 The FishTable (real item/name/icon ids from ClientItemDefinitions.json)
| Model | Name | Item def id | Name-string id (@32) | Icon id | Size |
|-------|------|-------------|----------------------|---------|------|
| 1670 | Swurgle Fish | 2148 | 6170 | 4594 | 1 |
| 1671 | Calico Catfish | 2149 | 6171 | 4595 | 2 |
| 1672 | Globfish | 2147 | 6169 | 4593 | 3 |

Bobber model = **1063** (`fishing_bobber_bbe.adr`) — `SpawnProxiedFishingBobber.Unknown` is
the bobber **model id**; if 0 the client never creates the bobber and the following
`SpawnFishRun` null-derefs → **client crash**. Must be > 0.

---

## 7. Git history (branch `fishing-minigame`)

Latest at top. Tree is clean as of this handoff.
```
5d2655f Restore the wandering fish; remove the stacked school clumps
1d6c137 Spawn only the biter fish (remove motionless school)
9aa21e9 Document RewardBundle opcode-50 yellow-text packet format
8a1851b Show the yellow "item received" notification on catch
b3471a0 Reel-before-bite = miss; keep ambient fish actively swimming
99a0029 Grant the caught fish to the inventory with a pickup popup
1efefac Remove the stationary catchable fish from the fishing scene
0e34a73 Document animation choreography (catchable frozen; drive ambient biter)
78dc9d5 Drive the bite/reel animations on an ambient fish, not the frozen catchable one
c576a4c Document Underwater_Bed positioning fix + real fish name/icon ids
6256066 Fix "fish in the sky": send the Underwater_Bed X per zone
3b0f13d Use real fish names/icons in the catch banner
```

---

## 8. Pending / next steps

1. **Live-test the latest changes** (commit `5d2655f`): the user hasn't yet confirmed that
   restoring the wanderers + removing the stacked schools looks right in-game. Verify:
   fish wander lively, no stacked clump in the center, yellow item-received text shows,
   catch banner correct.
2. **Uncertain:** whether the client's autonomous wander actually keeps `SpawnFishRun` fish
   moving, or if we need to periodically re-position them from the server. Confirm visually.
3. **DB persistence** — caught fish are **in-memory only** (`Player.GiveItem` does not
   persist; `SavePlayerToDatabase` doesn't save `Items`). Add persistence if desired.
4. **Per-zone fish tables** — currently 3 generic fish rolled uniformly
   (`RollCatch`, `TODO(fish-table)`). Build real per-zone loot tables with rarity weighting.
5. **Other zones** — only Sacred Grove (563) has a verified overworld spawn position; the
   other activities (560, 561, 562, 564, 565) have Underwater_Bed X set but placeholder
   overworld spawns. Verify/fill them in from the assets.

---

## 9. Where to look for wire-level detail

`FISHING_RE_NOTES.md` (~300 lines) has: the full opcode-138 sub-opcode table, every
`FishingResult` field offset with its client meaning, the fish-arena math, the animation
choreography (anim ids, splash composite-effect ids, the fish state machine `sub_CD1A10`),
the RewardBundle opcode-50 byte layout, and IDA function references (`sub_CCFDB0`,
`sub_CD12A0`, `sub_CD1A10`, `sub_B8A640`, `sub_B891F0`, `sub_AF85E0`, etc.).
