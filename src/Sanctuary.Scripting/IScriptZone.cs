using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting;

public interface IScriptZone : IScript
{
    int Id { get; }
    string Name { get; }
    ILogger Logger { get; }

    bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, [MaybeNullWhen(false)] out IScriptNpc npc);
}
