using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;

namespace Sanctuary.Scripting.Tests;

internal class MockScriptNpc(IScriptableZone _zone) : IScriptableNpc
{
    public ulong Guid { get => 0; init => throw new System.NotImplementedException(); }
    public string? Name { get => "MockNpc"; set => throw new System.NotImplementedException(); }
    public ConcurrentSet<string> Scripts { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public ILogger Logger { get; } = _zone.Logger;

    public IScriptableZone Zone => _zone;

    public void Say(string message)
    {
        throw new System.NotImplementedException();
    }

    public void SayLocalized(int stringId)
    {
        throw new System.NotImplementedException();
    }
}