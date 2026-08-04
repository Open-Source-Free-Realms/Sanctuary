using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting;

public interface IScriptZone
{
    int Id { get; }
    string Name { get; }
    ILogger Logger { get; }

    bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading);

    // Spawns every quest Collect-goal pickup (Quests.json CollectSpawns) as a world object. Returns the
    // number spawned. Called once from the zone script's onStart, same as the other spawnX calls.
    int SpawnQuestCollectibles();
}
