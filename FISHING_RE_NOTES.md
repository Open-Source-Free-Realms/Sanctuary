# Free Realms Fishing — Reverse Engineering Notes (working doc)

Sources of truth: `FreeRealms_2014-03-13` client binary (IDA: `FreeRealms_Admin.exe`), packet captures (no fishing traffic present — confirmed), video https://www.youtube.com/watch?v=lM7Pzhp9h6k
Server repo: this workspace (`src/`). Existing server fishing code matches wire layouts but game logic is guessed.

## Packet family — opcode 138 (0x8A), header: short opcode + short sub-opcode (LE)

Confirmed from `BaseFishingPacket::BaseFishingPacket` @ 0xB60910 (`m_nOpCode = 138`) and per-packet ctors:

| Sub | Name | Dir | Client handler evidence |
|---|---|---|---|
| 1 | UpdateData | S→C | u64 Guid, Vector4 Position (4 floats) |
| 2 | RegisterPlayerRequest | C→S | (client sends; payload = header only?) TBD |
| 3 | RegisterPlayerResponse | S→C | FishingPlayerConfig(9i+4f+3i), FishingZoneConfig(str+2i+f+2i+f? see below), List<int> ModelIds, List<ClientFishEntryInfo> |
| 4 | FishInfoUpdate | S→C | List<ClientFishEntryInfo> |
| 5 | CastAnimRequest | C→S (server relays to others) | u64 Guid, int, Vector4 Pos |
| 6 | CastRequest | C→S | u64 Guid, Vector4 Pos, bool |
| 7 | ReelInRequest | C→S (server relays) | u64 Guid, bool |
| 8 | SpawnProxiedFishingBobber | S→C | u64 Guid, int, Vector4 Pos, Vector4 Rot |
| 10 | UpdateProxiedFishingBobber | S→C | u64 Guid, int fishId, bool hooked, bool b2 — drives hook/escape/lost UI |
| 11 | SpawnProxiedFishingSchool | S→C | int SchoolId, Vec4 Pos, Vec4 Rot, List<schoolFish(3 ints)>, List<int> ModelIds, int, int |
| 12 | DespawnProxiedFishingSchool | S→C | int SchoolId |
| 13 | UpdateProxiedFishingSchool | S→C | int SchoolId, Vector4 Pos |
| 14 | FishingResult | S→C | u64 Guid, int ResultType, bool Caught, int FishId, int, string FishName, 7×int, string, string, int(+116), int(+124), bool(+128), int(+120) — NOTE: last 4 fields wire order = int,int,bool,int but struct offsets show the third int lands at +120 (read order on wire: +116, +124, +128(bool), +120) |
| 15 | SpecialRequest | C→S | u64 Guid, u64 Data |
| 16 | SpecialResponse | S→C | u64 Guid, int, bool |
| 18 | SpawnFishRun | S→C | bool, int ModelId, string TextureAlias, List<UnderwaterFishSpawnInfo> |

Sub 9 and 17 are not handled by the client (gaps, likely deprecated).

Key client funcs:
- `FishingProcessor::OnRoutePacket` @ 0xB6CD80 — parses all S→C, huge switch. Dump: scratchpad/FishingProcessor_OnRoutePacket.c
- Field readers dumped to scratchpad/fishing_readers*.c
- FishingResult(14) inner switch on ResultType, cases 0–8:
  - 0 = nothing caught ("bbe_fishing_nothing_caught"), 1 = ?, 2 = ?, 4 = caught-fish presentation (attaches fish to line "mouth"/"hinge_2"), 5 = scored catch (FishScoringData list gets 7 ints; shows name), 6/7 = "Fishing:EndCasting", 8 = special (StringHash 730504514, fallback stringId 436799)
- ClientFishEntryInfo: int FishType, int FishNameId(string id), int FishIconId, bool, bool FishSpecial, int FishLureRequirement, string, int, bool FishCatchable, int
- UnderwaterFishSpawnInfo: int, int ModelId, string TintAlias, string TextureAlias, int5, int6, bool7, int8..int17, float18

## MAJOR FIND: server-side fishing logic embedded in binary (~0x1076000–0x1090000)

`sub_107B140` = ServerFishingPlayerConfig loader from `bbe_FishingPlayerConfig.txt` with ALL semantic names (struct offsets):
```
+20 LineSnapChance            +24 MinTimeBeforeFishEscapes    +28 MinTimeBeforeFishStopsNibbling
+32 BobberMinDistanceFromPlayer  +36 BobberRunAgroundCatchMaxDistanceFromPlayer
+40 ReelSpeedMetersPerSecond  +44 HookingReelSpeedMetersPerSecond
+48 LineSnapTensionMinPercent +52 FishEscapeTensionMaxPercent
+56 MinTimeBeforeSpecial      +60 MinTimeBetweenSpecials
+64 SuperReelSpeedMetersPerSecond +68 SuperReelDuration       +72 MaxNibbleTime
+76 FishMaxDistanceFromHook   +80 FishStartDistanceFromHook
+84 BobberModelId(int)        +88 BobberSplashCompositeEffectId(int)
+92 MinCastDistance           +96 MaxCastDistance
+100 MinTargetRadius          +104 MaxTargetRadius            +108 PerfectDistance
+112 MinBiteChance            +116 MaxBiteChance              +120 TensionMeterSize
+152 CameraDistance +156 CameraPitch +160 CameraHeading +164 CameraTargetHLQ
```
- `sub_1079260` = FishingZoneConfig loader (`bbe_FishingZoneConfig.txt` + `.Node.txt` + `.SchoolPath.txt` + `.School.txt`)
- `sub_107AAE0` = another config consumer (same field names)
- FishingErrors.txt users: 0x1079260 area + 0x1079D80
- Server-side RTTI cluster @ 0x1ba2xxx: FishingSchoolInstanceDefinition, FishingSchoolPathDefinition (HashListMap16), FishingSchoolPathNodeDefinition (HashList64), FishingZoneConfig, FishingPlayerConfig
- Player skill stats (client strings @0x175aab8): FishingPerfectCastSkill, FishingLuck, FishingReelingSpeed, FishingLineStrength, FishingCastingStrength, FishingCastingSkill (no direct code xrefs — likely used via stat-name lookup tables)

## Client-side classes
- FishingProcessor (vft 0x1822a44), ControllerFishing (vft 0x181c2c4, 31 virtuals)
- ProxiedFishingPlayer / ProxiedFishingSchool / ProxiedFishingUnderwaterFish / ProxiedFishingBobber
- UI: FishingDataSource ("BaseClient.FishingProcessor.FishTypes"), FishingScoringDataSource (".FishScored"), FishingLureDataSource (".FishingLures")
- GUI events: FishingStatusText:Show/DisplayText, FishingCaughtStatusText, FishingDistanceMeter:FishHooked, Fishing:StartCasting/EndCasting/StartUnderwater/EndUnderwater
- Loading gate: "WaitForWorldReady: waiting for fishing processor" — client's world-ready wait includes fishing registration; if server never sends RegisterPlayerResponse in a fishing zone, loading screen never drops.

## Client fishing flow (from FishingProcessor::Process @0xB6C290, sub_B6BB90, OnRoutePacket)

Entering fishing:
1. Server sets character state bit 0x400000 (UpdateCharacterState) → `BaseClient::HandlePlayerUpdatePacket` calls `FishingProcessor::SetIsInFishing(true)` @0xB6BB90.
2. Client preloads models 1698/1700/1697/1699 (rod/line gear) and 1624/1670/1671/1672 (underwater fish), switches to ControllerFishing, hides nameplates, **sends RegisterPlayerRequest (sub 2, payload = u64 playerGuid)**.
3. World-ready loading loop (`BaseClient::sub_936D00`) blocks until FishingProcessor "pending registration" flag clears — cleared by receiving **RegisterPlayerResponse (sub 3)**. Server MUST respond or loading screen never drops.

Client state machine (state = FishingProcessor+0x?? "GAP_2[3]"), per-frame Process():
- **0 aiming**: raycast camera→water (physics hit type 8 = water). dist = |hit - player|; valid if MinCastDistance ≤ dist ≤ MaxCastDistance (packet PlayerConfig fields n2, n3 as floats). power = (dist-min)/(max-min). On cast press → anim state 1 ("Fishing:StartCasting", movement disabled).
- **1**: stores target/distance, **sends CastAnimRequest (sub 5: guid, int = distance-as-float-bits, Vector4 = direction)** relayed by server to other players; starts local cast animation → state 2.
- **2**: when anim progress ≥ 0.2 → **sends CastRequest (sub 6: guid, Vector4 = target pos, bool = ?"valid/normal cast" flag GAP_5[22])**; computes local landing point using packet PlayerConfig n1 (float: forward offset multiplier) and ZoneConfig field6 (water surface Y).
- **3**: waits for server **SpawnProxiedFishingBobber (sub 8)** (OnRoutePacket case 8: attaches bobber sim; uses PlayerConfig n7 and 3.0f and zoneWaterY and n1) — after 2s grace, sets proxied anim 4 → state 4.
- **4 bobber out / waiting bite**: server drives fish via **UpdateProxiedFishingBobber (sub 10: guid, int fishIndex, bool hooked, bool lost)**:
  - hooked=false, lost=false → fish nibbling (starts nibble anim on fish index)
  - hooked=true → fish hooked state (tension UI "FishingDistanceMeter:FishHooked")
  - hooked=false, lost=true → escape/lost: shows "bbe_fishing_fish_escaped" or "bbe_fishing_item_lost" (if flag on fish), unhooks meter.
  On reel press (with fish hooked or bobber aground) → **sends ReelInRequest (sub 7: guid, bool=1)**, proxied anim 5 → state 5.
- **5 reeling**: ticks fish anim; waits for server **FishingResult (sub 14)**.
- **6 result shown**: when anim done → anim state 0, movement re-enabled → state 0.

FishingResult(14) ResultType switch: 0=nothing caught ("bbe_fishing_nothing_caught", proxied anim 6), 1=?(clears cast), 2=fail-type (anim 6), 4=caught-item/fish attach presentation (attaches to "mouth"/"hinge_2", uses tint/texture aliases), 5=scored catch (fills FishScored row; shows catch), 6/7="Fishing:EndCasting", 8=special message (stringHash 730504514 / fallback 436799).

## FishingResult (sub 14) semantic layout (offsets in packet struct; wire = sequential)
```
+16 u64 PlayerGuid
+24 int ResultType (see above)
+28 bool Caught (drives moneyshot/attach variant)
+32 int FishNameId   (string table id — used for i18n lookup)
+36 int FishIconId
+40 string FishName  (raw string, used if id lookup fails?)
+56 int FishModelId
+60 int FishSize     (1=small 2=medium 3=large 4=extra_large; scales model 1x-4x)
+64 float TimeToCatch (seconds)
+68 int FishDifficulty
+72 int FishRarity
+76 int FishScore
+80 int ? (unused by scored UI; maybe special/loot id)
+84 string TintAlias
+100 string TextureAlias
+116 int ?  +124 int ?  +128 bool (checked as v200 in ResultType 1/8: "reset anim to idle")  +120 int ?
```
FishScored UI columns (sub_CD44E0): FishName, FishIconId, FishDifficulty, FishRarity, FishSize, FishScore, TimeToCatch.
FishTypes UI columns (sub_CD4420): FishType, FishName, FishIcon, FishSpecial, FishCatchable, FishLureRequirement (= ClientFishEntryInfo fields).

## Server FishingPlayerConfig DEFAULTS (ctor @0x107B6D0; bbe_FishingPlayerConfig.txt was server-side and is lost — these compiled defaults are ground truth)
```
LineSnapChance = 0.0                    MinTimeBeforeFishEscapes = 0.0
MinTimeBeforeFishStopsNibbling = 0.0    BobberMinDistanceFromPlayer = 0.5
BobberRunAgroundCatchMaxDistanceFromPlayer = 5.0
ReelSpeedMetersPerSecond = 2.0          HookingReelSpeedMetersPerSecond = 1.0
LineSnapTensionMinPercent = 0.9         FishEscapeTensionMaxPercent = 0.1
MinTimeBeforeSpecial = 3.0              MinTimeBetweenSpecials = 5.0
SuperReelSpeedMetersPerSecond = 4.0     SuperReelDuration = 1.0
MaxNibbleTime = 12.0                    FishMaxDistanceFromHook = 10.0
FishStartDistanceFromHook = 5.0         BobberModelId = 1063 (fishing_bobber_bbe.adr ✓)
BobberSplashCompositeEffectId = -1      MinCastDistance = 3.0
MaxCastDistance = 20.0                  MinTargetRadius = 0.2
MaxTargetRadius = 5.0                   PerfectDistance = 0.5
MinBiteChance = 0.0                     MaxBiteChance = 1.0
TensionMeterSize = 1.0                  (+124 unknown float = 2.0; +128..+148 six ints = 0)
CameraDistance = 6.0  CameraPitch = 0.445  CameraHeading = 1.85  CameraTargetHLQ = 0.2
```
Camera block matches the 4 floats (f10..f13) in the wire FishingPlayerConfig → wire packet config fields are a SUBSET of the server config. Known wire mappings: n1 = forward-offset multiplier (likely BobberMinDistanceFromPlayer or similar), n2 = MinCastDistance, n3 = MaxCastDistance, n7 = bobber-sim param, f10..f13 = camera block. (Exact full mapping pending server-side response writer.)

## DEFINITIVE: bite is SERVER-DRIVEN via UpdateProxiedFishingBobber (sub 10)

`UpdateProxiedFishingBobber` wire = [u64 Guid(player), int FishIndex, bool Flag1, bool Flag2].
FishIndex matches `UnderwaterFishSpawnInfo.Unknown` (the fish id) — resolved by `sub_B64710(fishIndex)` which walks the underwater-fish list matching `fish+20 == index`.

OnRoutePacket case 10 flag→fish-flag mapping (fish object byte offsets):
- **Flag1=true** → fish[+217]=1 (HOOKED), clears +216/+218/+220. → tension/"FishHooked" UI; fish anim state → 4 (splash CE 16265) → 5 (fighting). This is the bite/hook.
- **Flag1=false, Flag2=true** → fish[+216]=1 (INTERESTED). Fish swims toward hook & nibbles (anim state 1→2). Player-side nibble anim via sub_CCF6D0.
- **Flag1=false, Flag2=false** → fish[+218]=1 (ESCAPED/LOST), clears +216/+217. Shows "bbe_fishing_fish_escaped" or "bbe_fishing_item_lost" (if fish+16 set). Fish anim → 8 (gone).

Fish internal state machine `sub_CD1A10(fish, dtSeconds)` (state @ fish+32, anim @ fish+28):
1 = wander (initial post-spawn) → when +216 set, seeks hook → 2
2 = approach hook; when close & +217 set → 4; when +220 set → 1
3 = circle/retreat; +217→4; +220→1
4 = HOOK transition: plays splash composite effect 16265, → 5
5 = hooked/fighting; timer>~1.2 clears +217 sets +219; timer>threshold → 6
6 = nibble-wander/flee; +220→7; +218==1→8
7 = reeled toward player
8 = escaped/gone
Flags: +216 interested, +217 hooked, +218 escaped, +219 (transient), +220 landed/reeled-success.

**m_ProxiedFishingUnderwaterFish is ONLY populated by SpawnFishRun (sub 18).** Without it, `m_ProxiedFishingUnderwaterFish.m_head == null` and the reel path in Process case 4 can't fire → no catch possible. So the server MUST send SpawnFishRun after the cast to make a catchable fish.

`sub_CD1720(fish)` = returns fish[+217] (the hooked flag). Reel gate in Process case 4:
`reelPressed && fishHead && (fishHead.GAP[12] || !hooked)`.

## Reconstructed REQUIRED server→client sequence for one catch (RE-grounded)
1. Recv RegisterPlayerRequest(2) → send RegisterPlayerResponse(3): FishingPlayerConfig (camera floats f10..13 = 6.0/0.445/1.85/0.2 matter for camera), FishingZoneConfig (Unknown6 → water Y baseline; Unknown3 → fish-run X baseline), FishModelIds, ClientFishEntries (≥1 catchable). Unblocks loading.
   Also send UpdateData(1) (player guid+pos). [SpawnProxiedFishingSchool(11) is ambient decoration — optional, and must be at/below water, not +2 above.]
2. Recv CastRequest(6) → send SpawnProxiedFishingBobber(8){Guid=bobberGuid, Position=cast target}; then SpawnFishRun(18){one UnderwaterFishSpawnInfo, ModelId∈{1670 thin,1671 med,1672 fat}, Unknown=fishId}.
3. After nibble delay → UpdateProxiedFishingBobber(10){Guid=player, FishIndex=fishId, Flag1=false, Flag2=true} (interested/nibble).
4. After hook delay → UpdateProxiedFishingBobber(10){..., Flag1=true} (hooked). Client shows tension UI; player reels.
5. Recv ReelInRequest(7) → send FishingResult(14){ResultType=5 (scored catch), Caught=true, FishId, FishName, scoring ints}. [If player never reels within escape window → send UpdateProxiedFishingBobber(0,0) escape + FishingResult ResultType=0 nothing-caught.]

## SERVER BUGS IDENTIFIED in current implementation
1. **SpawnProxiedFishingBobber.Guid = Random** (CastRequestHandler) — WRONG. Client `sub_B64300(guid)` looks the bobber's Guid up as the ProxiedFishingPlayer. Must be the **player's guid**. Because of this the `IsCurrentPlayer` check fails and the local player's state machine never advances to state 3 → fishing is stuck. Same applies to UpdateProxiedFishingBobber.Guid, UpdateData.Guid, FishingResult.Guid — all = player guid.
2. Never sends SpawnFishRun(18) → no catchable underwater fish → reel path unreachable → no catch.
3. Never sends UpdateProxiedFishingBobber(10) → fish never becomes interested/hooked.
4. ReelIn immediately returns FishingResult ResultType=1 (likely wrong; scored catch = 5), regardless of hook state.
5. RegisterPlayer hardcodes zone 563; ignores which activity the player joined.
6. Ambient schools spawn at spawnPos.Y + 2f (above water) — the folder-name bug. Should be at/below the water plane.

## Server implementation plan (state machine, RE-grounded)
Per-player FishingSession, ticked from zone tick (Player.UpdateEveryTick) or gateway loop:
- OnCast(target): send SpawnProxiedFishingBobber{Guid=player, Pos=target}; send SpawnFishRun{1 catchable fish id=1, model 1670/1671/1672}; state=BobberOut; interestAt=now+rand(1-3s)
- tick BobberOut→now>=interestAt: UpdateProxiedFishingBobber{player, fishId, Flag1=0,Flag2=1}; state=Nibbling; hookAt=now+rand(2-4s)
- tick Nibbling→now>=hookAt: UpdateProxiedFishingBobber{player, fishId, Flag1=1}; state=Hooked; escapeAt=now+~8s
- tick Hooked→now>=escapeAt: UpdateProxiedFishingBobber{player,fishId,0,0}(escape) + FishingResult{ResultType=0 nothing}; state=Idle
- OnReel: if Hooked → FishingResult{ResultType=5,Caught=true,...}; else nothing-caught; state=Idle

## IMPLEMENTATION STATUS (v1 — builds clean, full solution)
Done in this session:
- New `FishingSession` (Sanctuary.Gateway/Fishing/FishingSession.cs): per-player state machine (Idle→BobberOut→Nibbling→Hooked), server-driven bite via UpdateProxiedFishingBobber, resolves catch on reel.
- `FishingSessions` (FishingSessionState.cs rewritten): registry keyed by player guid + `Tick()` driven from GatewayService main loop.
- Handlers rewired: MiniGameStart creates session + records zone; Register uses recorded zone + real water-Y/X floats + fish entries for models 1670/1671/1672 + schools moved to waterY-0.5 (fixes "fish over water"); Cast→session.OnCast (bobber guid = PLAYER guid, +SpawnFishRun); ReelIn→session.OnReel; disconnect→FishingSessions.Remove.
- FishingZoneConfig.Unknown3/Unknown6 retyped int→float (client reads them as floats).

REMAINING TO VERIFY (needs live test + catch-sequence agent):
- FishingResult ResultType for a *successful* catch (using 5) and the 7-int scoring field mapping (FishName/IconId/Difficulty/Rarity/Size/Score/TimeToCatch order per client case-5).
- SpawnFishRun UnderwaterFishSpawnInfo.Unknown7 semantics (catchable vs ambient) and whether one fish suffices.
- Reel gate: confirm client allows ReelInRequest while hooked (Process case 4 gate).
- Bite timing values (interest 1-3s, hook after 2-4s, escape window 8s) — tune to feel.

## v2 — catch-sequence agent findings integrated (builds clean)
Confirmed by client-enforcement analysis (FishingProcessor::Process / OnRoutePacket / fish tick sub_CD1A10):
- **Cast validation gate**: aiming only fires if raycast distance ∈ [PlayerConfig.Unknown2, Unknown3] read as FLOATS. v1 had int 10/1 (≈0, inverted) → casting impossible. FIXED: Unknown2=3.0f (min), Unknown3=20.0f (max). Also requires attachment group 7 (fishing pole equipped) — client prerequisite.
- **Required S→C for a catch**: RegisterPlayerResponse(3) [sane cast dists + water Y + camera], SpawnProxiedFishingBobber(8) [guid=player, BEFORE fish run — case 18 derefs bobber], SpawnFishRun(18) [≥1 fish, FIRST must have Unknown7=1 catchable, sizeClass 1..4], FishingResult(14) ResultType=5.
- **UpdateProxiedFishingBobber(10) is COSMETIC** (not enforced): (0,1)=approach/nibble, (1,x)=strike/bite visual, (0,0)=escape. Reel is permitted anytime for a catchable head fish; the server adjudicates via FishingResult. We still send them for correct visuals.
- **FishingResult scoring map** (case-5 fill → columns FishName/IconId/Difficulty/Rarity/Size/Score/TimeToCatch): FishName=@40, IconId=@36(Unknown1), Difficulty=@68(Unknown5), Rarity=@72(Unknown6), Size=@60(Unknown3), Score=@76(Unknown7), TimeToCatch=@64(Unknown4). Held-fish size scale=@120(Unknown12, 1..4). Caught(@28)= "junk item" flag → **0/false = real fish scaled by size** (set Caught=false for a normal fish).
- **Client does NOT grant the item** — FishingResult is display-only. Inventory must be updated by a separate packet (TODO, follow-up).
- Client→server messages in the whole cycle: RegisterPlayerRequest(2), CastAnimRequest(5), CastRequest(6), ReelInRequest(7), SpecialRequest(15). No bite ack.

## TO TEST (needs user)
1. Restart Sanctuary.Gateway (running instance locks the old DLL; new build won't load until restart).
2. Equip a fishing pole, enter a fishing activity (560-565), cast, wait for the bite (~3-7s), reel.
3. Watch Gateway logs: register → cast → (interested→hooked timers) → reel → result.
Follow-ups after it visually works: grant the caught fish to inventory (separate packet), real per-zone fish/loot table, tune bite timing, verify ambient school placement in-water.

## Live test #2 (02:19-02:21): cast worked, client CRASHED on cast — root cause found
- Dispatch fix confirmed: server logged "Player 17 cast at <449,-65,365> flag=True". Register/zone/castanim/cast all deserialize now.
- Client hard-crashed right after cast (no lua/asset error at cast time; native fault). Root cause via IDA:
  - `SpawnProxiedFishingBobber.Unknown` (int, 2nd field) = the **bobber model id**, stored to ProxiedFishingPlayer+216.
  - Client `sub_CCFDB0` only constructs the bobber object (ProxiedFishingBobber via `sub_CD3CF0`, stored at +212=[16]) when that model id `> 0`.
  - We sent `Unknown=0` → bobber never created → `SpawnFishRun` (case 18) derefs the null bobber `[16]` (via `sub_CD3C90`/`sub_CD3CD0` = ptr+320) to position the fish at the hook → access violation → crash.
  - FIX (commit 9afdf74): send `Unknown = BobberModelId = 1063` (fishing_bobber_bbe.adr). Bobber packet + fish-run sent in-order over reliable channel, so the bobber object exists before the fish-run derefs it. Bonus: bobber now visually appears.
- Client logs dir: `C:\Users\bobya\AppData\Local\OSFRLauncher\Servers\EDITz's Local Server\Client\Logs` (FreeRealms.log, LuaErrors.log, ActorFailures.log, AssetFailure.log). Asset 404s for sg_fishing_medpond tiles are a separate asset-server issue, not the crash.

## Live test #3+#4: core loop works; two issues
### "Nothing Caught" (FIXED, commit 41e0bed)
Client shows the fish on the line as soon as we signal interest; its reel gate lets the player reel
whenever a catchable fish exists; server is authoritative. Our OnReel only caught in strict Hooked
phase, so reeling during Nibbling returned nothing. Now: reel during Nibbling|Hooked -> catch.

### "Fish/camera up in the sky instead of underwater" (POSITIONING — needs correct spawn coords)
`FishingProcessor::sub_B640E0` @0xB640E0 places the underwater fish at world position:
  X = ZoneConfig.Unknown3 + distance*ZoneConfig.Unknown2 ; **Y = -8.0 (HARDCODED)** ; **Z = 485.0 (HARDCODED)**
(during reel state 5, Y interpolates -8..-5). So the underwater "arena" is anchored at a FIXED world
Y=-8, Z=485 (only X is config-driven). We spawn the player in the overworld pond at (435, -64, 370)
where the water surface is ~Y=-65 → the fish arena renders 57 units ABOVE the real water = "in the sky".
Client Y/Z are immutable literals; no packet/config can move the fish to the overworld water.
=> FIX must be POSITIONAL: spawn the player in sg_fishing_medpond where the water is at Y≈-8 near Z≈485,
   and set ZoneConfig.Unknown3 to that water's X. Current (435,-64,370) is the wrong elevation/spot.
   Need the real sg_fishing_medpond fishing coordinates (no zone geometry on server side to derive them).
   Experiment to try: spawn ≈ (435, -8, 470) facing +Z, Unknown3=435 (risk: may break the working cast
   if no water at Y=-8 there — easily reverted in FishingActivityZones.cs).

## Fish movement params + catch banner (commit 040d25b)
### UnderwaterFishSpawnInfo Unknown8..17 are FLOATS (int 1 = 1.4e-45 ~0 = frozen fish)
Wire field -> fish tick meaning -> lively value (ambient value):
Unknown5=sizeClass int 1..4 | Unknown6 unused | Unknown7 catchable bool (catchable MUST be head entry)
Unknown8 approach time (>0.25) 1.5 | Unknown9 reel divisor 2.0 | Unknown10 nibble/flee speed 2.0(1.0)
Unknown11 unused 1.0 | Unknown12 reel base offset 1.0 | Unknown13 turn speed 3.0(2.0)
Unknown14 WANDER speed 1.0(0.75) | Unknown15 wander decel 0.5 | Unknown16 approach/wander turn 3.0(2.0)
Unknown17 wander idle-min 1.0(2.0) | Unknown18 wander idle-max 3.0(5.0)
Transitions: wander->approach when interested(216) set at end of state1; approach->bite when fish reaches hook AND hooked(217) set. `this+58`(reel numerator) and `this+85`(stop dist) are auto-computed, not from server.

### Bite: UpdateProxiedFishingBobber flags are EXCLUSIVE not additive
Must send interested(Flag2=true) FIRST (state1 only checks 216); sending hooked while wandering does nothing (it clears 216). Fish swims to hook then bites on arrival (hooked latches). Hardcoded: 1.2s into fight hooked auto-clears, 1.7s state5->6(nibble); reel works once in state6.

### FishingResult field offsets (verified) + why RT5 showed nothing
@16 guid(=local player) @24 ResultType @28 bool(false=show size+weight) @32 NAME string-table id(NOT model)
@36 int @40 string(unused) @56 float WEIGHT("%2.2f") @60 int SIZE 1..4 @64 float @68/@72/@76 scoring
@80 int held-fish MODEL — **MUST be >0 or NO banner** (we sent 0!) @84/@100 tint/texture @116 sparkle CE
@124 int @128 bool @120 int size class (LAST on wire).
3 gates block the banner (any one): @32 name id invalid->blank name; @80<=0 -> no show-off -> no banner block; guid != local player.
Full catch sequence: interested -> hooked -> (fish bites/fights) -> on ReelIn: RT4(drag anim, fish->player) -> ~1.8s -> RT5(catch banner + show-off, auto-returns camera). Optional RT7=EndCasting.

## SOLVED: "fish in the sky" = wrong Unknown3 (commit 6256066)
Client hardcodes underwater fish arena Y=-8, Z=485; only X = ZoneConfig.Unknown3 (+dist*Unknown2).
Every fishing map's `<zone>Areas.xml` (dumped client assets: C:\Users\bobya\Documents\Free Realms Unpacker\editz fr assets\FR Assets 2025-07-07) has an **Underwater_Bed** area — the dedicated underwater scene — at consistent X~55-74, Y~-2..0, Z~482-486. The hardcoded arena sits inside it (fish ~6-8 below the bed surface = underwater). Water heights DIFFER per map (sg pond -65, bw -2, sh 0) — the user was right that a single hardcode can't be universal; the trick is the client anchors to the per-map bed and only X varies.
Underwater_Bed X per activity: 560/562 bw_medpond=74, 561 bw_stream=58, 563 sg_medpond=68, 564 sh_medpond=55, 565 sh_stream=69.
FIX: FishingZoneConfig.Unknown3 = zone.UnderwaterBedX (was overworld pond X=435 -> fish above the -65 pond). Unknown6 stays the overworld water Y (bobber height where you cast). This is decoupled: bobber in overworld pond, fish in the underwater bed; the fishing camera dives to the bed.

## Real fish name/icon ids (ClientItemDefinitions.json), commit 3b0f13d
Swurgle Fish name 6170 icon 4594 | Calico Catfish 6171/4595 | Globfish 6169/4593. FishingResult @32=nameId, @36=iconId.

## Animation choreography (commit 78dc9d5) — catchable fish is FROZEN; animate an AMBIENT fish
Client sub_CD1A10: the catchable fish (Unknown7=true) has both +16=1 (catchable) AND starts in state 6.
- state 2 begins `if(+16){state=6}` — catchable can never approach.
- state 6 begins `if(+16) goto LABEL_93` — skips wander/nibble anim; only checks +220(reel)/+218(escape); NEVER checks +216/+217.
=> Sending interested/hooked to the catchable head fish does NOTHING. Only AMBIENT fish (Unknown7=false, spawn in state 1) do approach(2)/lunge-bite(4, anim 103 size1-2 / 104 size3-4 + splash CE 16265)/fight(5)/nibble(6, anim 151)/reel-up(7).
The 3-D "!"/"?" marks (15940-15955) are VESTIGIAL in this build (stored in sub_CD3A70, never read). Hooked cue = GUI meter/status text (already fires from packet handlers). Don't chase them.
Reel gate (Process case 4) requires the HEAD fish to be catchable (GAP[12]=+16) → keep a catchable head fish (frozen at hook) + drive an ambient biter.
"Current fish" UNK[8] (which RT4 reels) = head fish id at SpawnFishRun, OVERWRITTEN by each UpdateProxiedFishingBobber to its FishIndex. So aim the biter's bobber packets AND RT4 at the same ambient index.
RT4 reels only a fish in state 6 (post-fight, ~1.7s after hook; hardcoded dword_1B79D1C). Player/rod/camera anims are automatic from the packets (proxied anims 7001-7013 via sub_CCFFB0; no extra server packets needed).
SERVER SEQUENCE: cast → SpawnFishRun[catchable head id1 + ambient biter id2 (caught model) + wanderers] → UpdateBobber(interested,idx2) → ~1.5s → UpdateBobber(hooked,idx2) [lunge/bite/fight] → on ReelIn wait FightDurationMs(1.9s) → RT4 [reel-up] → RT5 [catch].

## Yellow "item received" text = RewardBundle opcode 50, sub-type 2 (commit 8a1851b)
Plain ClientUpdatePacketItemAdd (opcode 2) adds to bag SILENTLY (no bottom text). The yellow text is a
RewardBundle packet: opcode 50 → client OnTunneledClientPacket2 case 50 → ClientRewardManager sub_B8A640
→ switch on a byte type (1=bundle,2=single item,6/7=xp/coin bank). Type 2 → sub_B891F0 read → sub_B899F0
PlayItemReceivedNotification → sub_A0C3A0 AddItemReceived → GUI event "ItemReceived" (yellow, 6s).
Wire (27 bytes, LE): int16 opcode=50 | uint8 type=2 | int32 itemDefId | int32 itemDefId2(=itemDefId) |
int32 iconId(0=default) | int32 tintId(0=default) | int32 count | int32 trailing(0). All 27 bytes required.
DISPLAY-ONLY (read-only def lookup) — still grant the item via ClientUpdatePacketItemAdd. Sent from Player.GiveItem.

## Open questions
- [ ] Exact trigger/conditions for client sending CastRequest(6) vs CastAnimRequest(5), and bool meanings
- [ ] Who simulates bite timing in live FR (server sends UpdateProxiedFishingBobber → server-side sim)
- [ ] RegisterPlayerRequest(2) payload
- [ ] FishingResult(14) semantic mapping of the 7 ints + 2 strings (likely score/size/rarity/difficulty + tint/texture alias)
- [ ] SpecialRequest/Response (15/16) purpose ("special" = treasure/rare event timer in config)
- [ ] Server-side session tick logic in 0x107x–0x108x region (bite chance roll, tension, line snap)
- [ ] bbe_*.txt config assets — extract actual values from game assets (fr-adr-toolkit may read pack files)

## SpawnFishRun (sub 18) — the "stationary fish in the center" / Unknown2 decoy

`FishingProcessor::OnRoutePacket` case 18 @ 0xB6D3AB, per-fish creator `ProxiedFishingUnderwaterFish::sub_CD3A70` @ 0xCD3A70.

UnderwaterFishSpawnInfo.Unknown7 is the **catchable** byte (struct off +0x30). Per fish:
- **Non-catchable** → created as a wandering fish (random target, state 1/wander).
- **Catchable** → `sub_CD3A70` starts it in **state 6** (nibble-at-hook) and attaches the `!`/`?` alert
  composite effects sized by Unknown5 (1→15940/15941, 2→15950/15953, 3→15951/15954, 4→15952/15955).
- Loop rule @ 0xB6D4C4: a **non-head** fish that is catchable is **skipped** (not created); the head fish
  is always created; catchable fish are parked at the hook, non-catchable get a random wander pos.

After building the list, @ 0xB6D806 the client checks the **head** fish's catchable byte:
- **head NOT catchable** → `loc_B6D9F9`: spawns a **separate decoy actor** from the packet's `Unknown2`
  model (+ optional `Unknown3` texture) parked at the hook center, stored at proc+0x304/+0x308, plus the
  `sg_fishing_lure_bbe` lure. This un-animated decoy is the **"stationary fish in the center"**.
  Note: `Unknown2 = 0` **crashes** — `ModelDefinitionManager::sub_726E30(0)` returns null and the caller
  immediately does `mov esi,[eax+24h]` @ 0xB6DA36 (null deref). Do NOT zero it.
- **head catchable** → `hinge_2` branch (fish attached to the line hinge) with a clean null-actor skip; **no decoy**.

**Fix:** make the FIRST fish in the run catchable (Unknown7=true). This removes the decoy and gives the
native "fish nibbling at your lure" (state 6 + !/? bubbles). Keep a separate non-catchable biter fish to
retain the swim-in + lunge (state 2→4) bite animation; drive it via UpdateProxiedFishingBobber as before.

Bite-flag wiring confirmed from case 10 @ 0xB6DD83 → fish bytes: interested(Flag2)→fish+0xD8 (this+216),
hooked(Flag1)→fish+0xD9 (this+217, triggers state 2→4 lunge), neither→fish+0xDA (this+218, state 6→8 escape).

### Hiding the forced hook decoy (chosen fix)

The decoy actor at proc+0x304 is built from the packet's Unknown2 model; the only packet levers are
Unknown2 (model) and Unknown3 (texture). `ActorManager::CreateActor` @ 0x79D5F0 never returns null for a
bad model (geometry loads async), so the null-actor skip is unreachable; `SetTextureAlias` with a bogus
alias doesn't reliably hide it; scale/tint/position are not packet-controlled for the decoy.

Fix: set **Unknown2 = 69** (`widget_01.adr`, "Invisible Block" in ClientModelDefinitions) so the forced
hook actor has no visible mesh. It is registered (no `sub_726E30` null-deref crash) and invisible. The
wandering fish and the driven biter use their own model ids and are unaffected; the decoy still
self-despawns on bite, so this only hides the otherwise-visible pre-bite phase.

## Bobber / line placement

The bobber comes from SpawnProxiedFishingBobber (sub 8) @ OnRoutePacket case 8 (0xB6D1CA): it sets the
fishing player's bobber-model field (+0xD8 = packet Unknown) and calls `sub_CCFDB0`, which builds the
bobber actor + fishing line (`sub_CD42A0` → `sub_CD3E20` raycast) at the packet Position. The bobber
model/assets (fishing_bobber_bbe.adr, model 1063) load fine (confirmed in the client IndirectAssets log).

Bug: we were sending the bobber at the **overworld cast spot** (~<450, -65, 380>), but the fishing camera
frames the hardcoded underwater bed (X=UnderwaterBedX, Y=-8, Z=485), ~400 units away — so the bobber was
spawned off-camera. Fix: send the bobber at the bed's water surface (UnderwaterBedX, +2, 485). Y=+2 = the
arena floor (-8) + the client's surface-lure offset (dword_18227AC = 10).

## Fishing line (rod -> bobber) — why it only shows at the bite

Line render lives in the bobber tick `sub_CD0150` (called per ProxiedFishingPlayer from Process). It
builds ONE line: from the rod's `EMITTER2` socket to the bobber's `LINE` socket. The line target field
ProxiedFishingPlayer+0xD4 is set to the bobber object in `sub_CCFDB0`. The line object (+0x130) is created
once, when ALL hold: bobber(+0xD4) set, bobber actor has a `LINE` socket, and the proxied character's
attachment group 7 (the fishing rod) resolves (`GetProxiedCharacterAttachmentGroupManager` ->
`sub_72A110(7)` -> `sub_72F4D0`).

Case 8 (SpawnProxiedFishingBobber) calls `sub_B64580(this, IsCurrentPlayer ? 3 : 4)` — the CURRENT player
gets fishing-player state 3 (no `sub_CCFFB0` case), observers get state 4 (-> `sub_963DA0(char,1)`, which
just sets the 0x40 "fishing" bit). `sub_963DA0` does NOT attach the rod; group 7 is the player's equipped
pole mirrored onto the proxied character.

Empirical (live test): lengthening the approach to 5-7s did NOT reveal the line earlier — it still appears
exactly at the hooked update, not on a timer. So the gate is the hooked/reeling state (rod attachment /
reeling pose making the EMITTER2<->LINE endpoints valid), not elapsed time. Pre-bite line for the CURRENT
player is the local ControllerFishing cast, which the server-driven proxied path does not currently engage.
Open problem: get group-7 (rod) valid, or drive the local cast, before the bite.

## Correction: the current player DOES have a proxied character

The fishing-avatar experiment (route the bobber to a separate proxied char) CRASHED the client on
cast. That is diagnostic: `SpawnFishRun` (case 18, no guid — operates on the current player's fishing
entry) null-derefs the bobber if the current player's fishing entry has none. It only had one before
because case 8 (SpawnProxiedFishingBobber, player guid) created it — and case 8 REQUIRES
`GetProxiedCharacterByGuid(player)` non-null. So the current player DOES have a proxied character; the
"you're culled, you have no proxied char" theory was wrong, and the avatar approach is invalid (reverted).

Revised model of the remaining gap:
- The bobber object + actor and the rod->bobber line are built off the player's own proxied character.
- The line is gated on the hooked/reeling state (delay test proved it is bite-driven, not time-driven) —
  the rod's reeling pose is what makes the EMITTER2<->bobber-LINE endpoints valid. In live FR the taut
  line generally appears when the line has tension (reeling), so "no line before the bite" is plausibly
  partly normal.
- The bobber itself is built around state 4 (~2s post-cast) at whatever position we send; by then the
  camera has dived to the far underwater bed, so a world-position bobber is off-camera and a bed-position
  bobber only appears ~state4≈bite. Making the bobber appear during the brief pre-cam world cast needs it
  created at cast time (proxied char ready at the cast instant), which the client may not satisfy yet.

## RegisterPlayerResponse model-id list = scene preload (FishModelIds)

`FishingProcessor::sub_B68600` (called from the case-3 register handler) takes the response's model-id
list and, for EACH id, looks up the model and `CreateActor`s it, storing the actor in `m_ActorIds`
(FishingProcessor+0x270). `m_ActorIds` is otherwise only touched by the ctor/dtor — so this list is a
**preload/instantiate set**, not a per-frame thing. We were sending only the 3 underwater fish
(1670-1672), so the bobber (1063), lure (1673, sg_fishing_lure_bbe) and catch models streamed in lazily,
which fits the bobber/line only appearing around the bite. Fix: preload the full set
(FishingSession.PreloadModelIds = bobber + lure + underwater fish + catch fish). Watch for stray preload
actors at the world origin (CreateActor with no transform set); the 3 fish already did this unnoticed.

Fishing model map (Models.txt): 1063 fishing_bobber_bbe (animated bobber); 1443 sg_fishing_pole_01_bbe
(rod); 1596-1619 sg_2fish/3fish_trout_{big,biggest,med}_{3,6,9,12}meter (reel-distance display); 1620-1623
sg_fish_catch_{large,medium,xlarge,small}_bbe (held catch); 1624 treasure chest; 1641-1645
sg_fishing_bobber01-05_bbe (in-world bobber variants); 1670-1672 fishing_{thin,medium,fat}_fish_bbe
(underwater minigame fish); 1673 sg_fishing_lure_bbe (in-world lure); 1690-1691 + 1697-1699 reticles.

## BREAKTHROUGH: the bobber was invisible due to a zero-scale matrix (fixed)

SpawnProxiedFishingBobber's SECOND Vector4 is NOT a quaternion — it is a FORWARD DIRECTION. Case 8 of
OnRoutePacket drops the two vectors into a matrix: row 2 = the "Rotation" vector (forward/Z axis), row 3
= Position (translation). Then `sub_770A70` @ 0x770A70 orthonormalizes it: it normalizes the rows and
REBUILDS the basis via cross products seeded from row 2. We were sending Rotation = (0,0,0,1), whose xyz
direction is (0,0,0), so every cross product came out zero and the ENTIRE 3x3 basis collapsed to zero —
the bobber matrix was zero-scale, i.e. placed at the correct position but rendered at size zero
(invisible). This is why the bobber was never visible anywhere (and it showed fine at the world origin
via the FishModelIds preload, because that path uses a normal identity transform).
Fix: send Rotation = (0,0,1,0) (a real +Z forward). Any non-zero xyz direction works. Verified the
math: forward=(0,0,1) → row0=cross((0,0,1),(0,1,0))=(-1,0,0), a valid orthonormal basis.
The wire layout of the packet is correct: Guid(8) + model(4) + Vector4 Position + Vector4 Rotation
(client reader FishingPacketSpawnBobber_Read @ 0xB66A00, two sub_8E2410 Vector reads).

## The rod->bobber line (sub_CD0150) and its group-7 gate

`sub_CD0150` (bobber tick, called per fishing player from Process) is the ONLY place the rod->bobber line
is built. It creates the line object ONCE (ProxiedFishingPlayer+0x130 == 0) when ALL hold:
- bobber object exists (ProxiedFishingPlayer+0xD4, set by sub_CCFDB0 at cast),
- the bobber ACTOR exists (bobber+24 = actor id) and has a `LINE` socket (Actor::sub_77A130),
- the fishing player's proxied character resolves (GetProxiedCharacterByGuid(m_llGuid @ +0x10 = our guid)),
- **`BaseAttachmentGroupManager::sub_72A110(mgr, 7)` → `BaseAttachment::sub_72F4D0` resolves, and its
  `EMITTER2` socket** — i.e. the fishing pole rig as attachment GROUP 7 on the proxied character.
The line runs rod EMITTER2 socket -> bobber LINE socket via sub_B62F30. `sub_10C1270` just clamps a 0..1
alpha onto it each tick; `sub_CD3F80` is the bobber bob/splash (uses sub_770A70 + a landing composite
effect). Neither gates line creation — the gate is purely group 7.
`ProxiedCharacter::sub_963DA0` sets a 0x40 "fishing" bit (which the pole/group-7 rig rides on); it is set
by `sub_CCFFB0` for proxied-fishing-player states 1/2/4/6. State 2 = cast pose (CastAnimRequest, case 5,
which does `sub_B64580(this,2)` → `sub_CCFFB0(player,2)`). We relay CastAnimRequest to self (sendToSelf:
true) so the rig attaches from the cast — the client already sends CastAnimRequest (confirmed in the
gateway log), so the rod IS on from the cast.

RESOLVED — it was a TIMING/masking issue, not a missing rig. `IsCurrentPlayer` (`m_pBaseClient->
m_pProxiedCharacter == this`) is ALWAYS true for us, so `SpawnProxiedFishingBobber` (case 8) runs
`sub_B64580(this, IsCurrentPlayer ? 3 : 4)` = **state 3**, and state 3 is a NO-OP in `sub_CCFFB0` (no
case 3). So the bobber-spawn packet never sets the fishing bit or builds the line for us; it just sets
FishingProcessor state 3 and relies on the client's LOCAL `Process` case 3 to reach **state 4**
(`sub_CCFFB0(player,4)` → `sub_CCFDB0` recreates the bobber into a line-ready form + sets the bit)
~2s later. `sub_CD0150` only builds the line on THAT state-4 bobber. The old bite timeline (interest at
spawn+1-2s, bite at spawn+~3s) landed the bite right on the spawn+2s state-4 mark, so the line only
seemed to appear "at the bite." Fix: in `OnCast`, delay interest to spawn+3.5-5s so the line settles
first. NOTE: do NOT preload the bobber model — that would let the line build on the case-8 bobber, which
state 4 then destroys (`sub_CCFDB0` frees the old bobber at its top), orphaning the line.

## Debug: /tporigin

Type `tporigin` (NO leading slash — the client swallows unknown /commands) in chat to teleport to (0,0,0);
`tp <x> <y> <z>` for arbitrary coords. Handler: PacketChatHandler.TryHandleCommand via
ClientUpdatePacketUpdateLocation(Teleport=true). Used to confirm the FishModelIds preload actors (fish +
whatever else) park at the world origin unpositioned, and that the bobber model itself renders.
