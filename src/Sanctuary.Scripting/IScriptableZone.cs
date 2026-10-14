using System.Diagnostics.CodeAnalysis;

namespace Sanctuary.Scripting;

public interface IScriptableZone : IScriptable
{
    IScriptManager ScriptManager { get; }

    int Id { get; }
    string Name { get; }

    void SetClientAreaEnabled(int areaId, bool enabled);

    void Broadcast(string message);

    void BroadcastNear(string message, float x, float z, float radius);

    bool IsSeasonActive(string seasonName);

    bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, float pitch, float roll, [MaybeNullWhen(false)] out IScriptableNpc npc);
}
