using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public class CombatEncounter
{
    private const int EncounterZoningId = 0;
    private const byte EncounterZoningUnknown = 3;
    private const int EncounterZoningGeometryId = 96;

    public MiniGameUiDefinition Definition { get; init; } = new();

    public int ZoneDefinitionId { get; init; }

    public Vector4 EntryPosition { get; init; }

    public Quaternion EntryRotation { get; init; }

    public List<CombatEncounterSpawn> Spawns { get; init; } = [];

    public virtual void OnGo(Player player, MiniGameUiSession miniGameUiSession)
    {
        if (player.Mount is not null)
            player.Dismount();

        if (!player.TeleportToZone(ZoneDefinitionId, player.Guid, EntryPosition, EntryRotation, EncounterZoningId, EncounterZoningUnknown, EncounterZoningGeometryId, out var zone))
            return;

        if (!zone.Npcs.Any())
            SpawnNpcs(zone);

        miniGameUiSession.State = MiniGameUiState.Entering;
    }

    private void SpawnNpcs(IZone zone)
    {
        foreach (var spawn in Spawns)
            zone.TrySpawnNpc(spawn.NpcId, default, spawn.Position.X, spawn.Position.Y, spawn.Position.Z, spawn.Heading, default, default, out _);
    }

    public virtual void OnClose(Player player, MiniGameUiSession miniGameUiSession, IZoneManager zoneManager)
    {
        player.TeleportToZone(zoneManager.StartingZone, player.StartingZonePosition, player.StartingZoneRotation);
    }
}
