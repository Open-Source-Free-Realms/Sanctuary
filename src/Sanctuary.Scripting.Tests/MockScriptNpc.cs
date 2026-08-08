using System;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptNpc(IScriptableZone _zone) : IScriptableNpc
{
    public ulong Guid { get => 0; init => throw new NotImplementedException(); }
    public string? Name { get => "MockNpc"; set => throw new NotImplementedException(); }
    public ConcurrentSet<string> Scripts { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public ILogger Logger { get; } = _zone.Logger;

    public IScriptableZone Zone => _zone;

    public ScriptContext GetOrCreateScriptContext()
    {
        throw new NotImplementedException();
    }

    public void Say(string message)
    {
        throw new NotImplementedException();
    }

    public void SayLocalized(int stringId)
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
}