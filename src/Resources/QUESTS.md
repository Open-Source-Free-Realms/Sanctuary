# Adding Quests

Quests are entirely data-driven: everything from goals to rewards to NPC gating lives in
[`Quests.json`](Quests.json). Adding a quest means adding a JSON entry - no C# code changes
required, unless the goal type you need isn't wired yet (see [Goal types](#goal-types) below).

`Quests.json` is loaded once at startup by `QuestDefinitionCollection.Load` (called from
`ResourceManager`) into `src/Sanctuary.Game/Resources/Definitions/QuestDefinition.cs` /
`QuestGoal.cs` - read those two files for the authoritative field list. This doc is a guide for
using them, not a substitute.

## Before you start

`GiverGuid` and `TargetGuid` must be the guid of an NPC already spawned in a zone script (the
`100000000000 + definitionId` convention used by `zone.spawnNpcWithGuid` calls, e.g.
`src/Scripts/Zone/FabledRealms.lua`) - not the `Npcs.json` definition id itself.

There's no validation: if the guid doesn't match a spawned NPC, the quest silently can never be
offered or turned in - no error, no log. `/npc spawn <definitionId>` (admin-only) prints a guid you
can use for ad hoc testing.

## Minimal quest

The smallest valid entry is a single "talk to this NPC" quest with no reward:

```json
{
  "Comment": "My New Quest",
  "QuestId": 5000,
  "TitleId": 0,
  "DescriptionId": 0,
  "GiverDialogueId": 0,
  "ObjectiveDescriptionId": 0,
  "SubGoalId": 0,
  "TargetDialogueId": 0,
  "IconId": 43278,
  "GiverGuid": 100000001201,
  "TargetGuid": 100000001201,
  "RewardCoins": 0,
  "RewardExperience": 0,
  "RewardItems": [],
  "PrerequisiteQuestId": 0,
  "NextQuestId": 0
}
```

`Comment` is ignored by the loader - it's purely a label for whoever's reading the file. `QuestId`
must be unique; a duplicate is skipped with a startup warning. `IconId` 43278 is what every
existing quest uses.

With no `Goals` array, the quest falls back to a single synthesized "talk to `TargetGuid`" goal
built from `ObjectiveDescriptionId`/`SubGoalId`/`TargetDialogueId` (see
`QuestDefinition.EffectiveGoals`). This is the legacy shape and still works fine for simple
give-and-turn-in-at-the-same-NPC quests (see `QuestId: 3001` "Nomi's Little Brother" style quests
in `Quests.json` for a real single-goal example, or `3010`/`3011` for a real multi-step chain).

## Multi-goal quests

For anything with more than one step, use `Goals` - an ordered checklist. Goals complete in order;
the active goal is the first incomplete one, and the quest is ready to turn in once every goal is
done. Each goal becomes its own tracker row.

```json
"Goals": [
  { "NameId": 100103, "DescriptionId": 100104, "DialogueId": 384144, "TargetGuid": 100000003018 },
  { "NameId": 94511,  "DescriptionId": 94512,  "DialogueId": 94257,  "TargetGuid": 100000002049 }
]
```

- `NameId` is the tracker row text AND the goal's client-side identity - it must be unique across
  a quest's goals, or the client can't tell them apart (checkmarks/progress won't render right).
  The loader warns on a duplicate.
- `DialogueId` on the **final** goal becomes the turn-in speech bubble; earlier goals' `DialogueId`
  is unused today.
- `Type` defaults to `0` (TalkToNpc) if omitted.

## Goal types

| Type | Value | Completes when... | Wired? |
|---|---|---|---|
| `TalkToNpc` | 0 | player interacts with `TargetGuid` | Yes |
| `ReachLocation` | 1 | player gets within `ReachRadius` (default 12) of `ReachPosition`, checked on every position update (2D, X/Z only) | Yes |
| `Collect` | 2 | player gathers `RequiredCount` pickups from `CollectSpawns` | Yes |
| `Kill` | 3 | player defeats `RequiredCount` NPCs matching `KillNpcNameId`/`KillNpcNameIds` | **No** - `QuestManager.OnNpcKilled` exists but nothing calls it (no combat system on this branch) |
| `EncounterComplete` | 4 | player wins the battle instance matching `EncounterId` | **No** - same reason as `Kill` |

Don't ship a quest using `Kill` or `EncounterComplete` goals right now - a player who accepts one
has no way to ever complete it.

### ReachLocation example

Use `/whereami` in-game to grab coordinates while standing where you want the goal to trigger.

```json
{ "NameId": 93294, "Type": 1, "ReachPosition": [-690.38, 2.3, -1060.25], "ReachRadius": 15 }
```

### Collect example

```json
{
  "NameId": 46489,
  "Type": 2,
  "RequiredCount": 8,
  "CollectModelId": 584,
  "CollectNameId": 74449,
  "CollectSpawns": [
    [-1090.0, 5.40, 384.0],
    [-1072.0, 5.30, 348.0]
  ]
}
```

`RequiredCount` of `0` (or omitted) defaults to "collect them all" (`CollectSpawns.Count`).
`CollectModelId` is a `Models.txt` id (e.g. `93` = `bw_collectible_mushrooms_01`); `CollectNameId`
is the hover/name text shown on the pickup.

Every zone that has a Collect-goal quest must call `zone.spawnQuestCollectibles()` once from its
Lua `onStart` (see `FabledRealms.lua`). This one call spawns **every** collectible pickup across
**all** quests in `Quests.json`, not just that zone's - it isn't scoped, so a zone without any
collect quests can skip the call entirely and it's harmless either way.

## Chaining and gating

- `PrerequisiteQuestId`: must be completed before this quest can be offered. `0` = none.
- `NextQuestId`: purely cosmetic automation - once this quest completes, the next quest's giver
  badge refreshes immediately (no relog needed) so players see it's available right away. It does
  **not** replace setting that quest's own `PrerequisiteQuestId`.
- `ExcludesQuestIds`: quests that block this one while active *or* completed, and vice versa - list
  both directions. Used for the two race-specific "Introduce Yourself" quests (`2563`/`2564`) so a
  player only ever gets one. Abandoning a quest clears it from the player's quest state, which lifts
  the exclusion automatically.

## Rewards

- `RewardCoins`, `RewardExperience` - plain ints, granted on completion.
- `RewardItems` - a list of item definition ids added to the player's bags. These **are**
  validated against the item definitions at grant time; an unknown id won't grant (check the
  server log if a reward item silently doesn't show up).

## Badges

`NotificationAvailable` (default `2`, the "!" icon) and `NotificationActive` (default `6`, the "?"
icon) control the world badge shown above the giver/target's head. You'll rarely need to change
these - the defaults match every existing quest.

## Text ids

`TitleId`, `DescriptionId`, `GiverDialogueId`, goal `NameId`/`DescriptionId`/`DialogueId`, etc. are
SOE T4 client localization ids (resolved client-side as `Global.Text.<id>`). There's no
server-side validation - any int is accepted - but an id with no matching client string just shows
as unresolved text in-game. You need ids that already exist in the client's string table; this
repo can't add new client-side strings.

## No database migration needed

`CharacterQuests` stores `QuestId, CharacterId, Completed, GoalProgress, GoalCount` generically -
adding quests to `Quests.json` is purely additive and needs no EF Core migration.

## Checklist

1. Confirm the giver/target NPCs are already spawned in a zone script - note their guids.
2. Pick a unique `QuestId`.
3. Write the entry: single goal (legacy fields) or a `Goals` list for multi-step.
4. Wire `PrerequisiteQuestId`/`NextQuestId` if it's part of a chain, `ExcludesQuestIds` if it's
   mutually exclusive with another quest.
5. If it has a `Collect` goal, make sure the zone's Lua script calls
   `zone.spawnQuestCollectibles()` from `onStart`.
6. Avoid `Kill`/`EncounterComplete` goals until a combat system exists to drive them.
7. Build, spawn any test NPCs with `/npc spawn`, and walk through accept -> goals -> turn-in
   in-client.
