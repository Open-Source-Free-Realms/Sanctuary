using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptZone(ILogger _logger) : IScriptZone
{
    public int Id => 0;

    public string Name => "MockZone";

    public ILogger Logger { get; } = _logger;

    public bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, [MaybeNullWhen(false)] out IScriptNpc npc)
    {
        throw new System.NotImplementedException();
    }
}