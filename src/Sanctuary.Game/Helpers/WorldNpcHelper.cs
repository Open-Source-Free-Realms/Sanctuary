using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Routes referee NPC edits to the database when the NPC is a persisted world NPC (see WorldZone.Npcs).
/// NPCs anywhere else are only changed in memory.
/// </summary>
public static class WorldNpcHelper
{
    /// <summary>
    /// Saves the NPC's current state. Returns false if it is not persisted.
    /// </summary>
    public static bool Save(Npc npc) => npc.Zone is WorldZone worldZone && worldZone.TrySaveWorldNpc(npc);

    /// <summary>
    /// Makes the NPC's current position its home, so it is saved and respawns there.
    /// </summary>
    public static bool SavePosition(Npc npc)
    {
        npc.SpawnPosition = npc.Position;
        npc.SpawnRotation = npc.Rotation;

        return Save(npc);
    }

    /// <summary>
    /// Removes the NPC, deleting it from the database if it is persisted.
    /// </summary>
    public static void Remove(Npc npc)
    {
        if (npc.Zone is WorldZone worldZone && worldZone.TryDeleteWorldNpc(npc))
            return;

        npc.Dispose();
    }
}
