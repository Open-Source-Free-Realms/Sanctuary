using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptNpc(IScriptZone _zone) : IScriptNpc
{
    public ulong Guid { get => 0; init => throw new System.NotImplementedException(); }
    public string? Name { get => "MockNpc"; set => throw new System.NotImplementedException(); }
    public ConcurrentSet<string> Scripts { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public ILogger Logger { get; } = _zone.Logger;

    public IScriptZone Zone => _zone;

    public void Say(string message)
    {
        throw new System.NotImplementedException();
    }

    public void SayLocalized(int stringId)
    {
        throw new System.NotImplementedException();
    }
}