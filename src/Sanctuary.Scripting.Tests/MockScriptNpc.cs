using System;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Actions;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptNpc(IScriptableZone _zone) : IScriptableNpc
{
    public ulong Guid { get => 0; init => throw new NotImplementedException(); }
    public string? Name { get => "MockNpc"; set => throw new NotImplementedException(); }
    public ILogger Logger { get; } = _zone.Logger;
    public ScriptRuntime ScriptRuntime => _zone.ScriptRuntime;
    public IScriptableZone Zone => _zone;
    public (float x, float y, float z) Position => (0, 0, 0);

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

    public IAction MoveTo(float x, float y, float z, bool direct)
    {
        throw new NotImplementedException();
    }

    public void SetAction(string slot, IAction action)
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