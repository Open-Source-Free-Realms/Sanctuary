using System.Diagnostics.CodeAnalysis;

namespace Sanctuary.Scripting;

public interface IScriptableZone : IScriptable
{
    IScriptManager ScriptManager { get; }

    int Id { get; }
    string Name { get; }

    bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, [MaybeNullWhen(false)] out IScriptableNpc npc);
}
