using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptZone(ILogger _logger) : IScriptableZone
{
    public int Id => 0;

    public string Name => "MockZone";

    public ILogger Logger { get; } = _logger;

    public ScriptContext GetOrCreateScriptContext()
    {
        throw new System.NotImplementedException();
    }

    public bool TryAddScript(string scriptName)
    {
        throw new System.NotImplementedException();
    }

    public bool TryRemoveScript(string scriptName)
    {
        throw new System.NotImplementedException();
    }

    public bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, [MaybeNullWhen(false)] out IScriptableNpc npc)
    {
        throw new System.NotImplementedException();
    }
}