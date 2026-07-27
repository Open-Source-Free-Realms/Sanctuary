using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;

namespace Sanctuary.Scripting;

public interface IScriptNpc
{
    public ulong Guid { get; init; }
    public string? Name { get; set; }
    public ConcurrentSet<string> Scripts { get; set; }
    public ILogger Logger { get; }
    public IScriptZone Zone { get; }

    void Say(string message);
    void SayLocalized(int stringId);
}
