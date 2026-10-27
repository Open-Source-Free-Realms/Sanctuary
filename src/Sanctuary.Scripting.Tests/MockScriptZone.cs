using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptZone(ILogger _logger) : IScriptableZone
{
    public IScriptManager ScriptManager => throw new NotImplementedException();

    public int Id => 0;

    public string Name => "MockZone";

    public ILogger Logger { get; } = _logger;

    public ScriptRuntime ScriptRuntime { get; } = new ScriptRuntime(_logger);

    public ScriptContext GetOrCreateScriptContext()
    {
        throw new NotImplementedException();
    }

    public bool TryAddScript(string scriptName)
    {
        throw new NotImplementedException();
    }

    public bool TryRemoveScript(string scriptName)
    {
        throw new NotImplementedException();
    }

    public void SetClientAreaEnabled(int areaId, bool enabled)
    {
        throw new NotImplementedException();
    }

    public void Broadcast(string message)
    {
        throw new NotImplementedException();
    }

    public void BroadcastNear(string message, float x, float z, float radius)
    {
        throw new NotImplementedException();
    }

    public bool IsSeasonActive(string seasonName)
    {
        throw new NotImplementedException();
    }

    public bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, float pitch, float roll, [MaybeNullWhen(false)] out IScriptableNpc npc)
    {
        throw new NotImplementedException();
    }
}