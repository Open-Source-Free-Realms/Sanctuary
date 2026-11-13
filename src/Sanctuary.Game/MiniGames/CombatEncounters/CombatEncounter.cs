using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public class CombatEncounter
{
    public MiniGameUiDefinition Definition { get; init; } = new();

    public int ZoneDefinitionId { get; init; }

    public Vector4 EntryPosition { get; init; }

    public Quaternion EntryRotation { get; init; }

    public List<CombatEncounterSpawn> Spawns { get; init; } = [];

    public void SpawnNpcs(IZone zone)
    {
        foreach (var spawn in Spawns)
            zone.TrySpawnNpc(spawn.NpcId, default, spawn.Position.X, spawn.Position.Y, spawn.Position.Z, spawn.Heading, default, default, out _);
    }
}
