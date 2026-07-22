using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting;

public interface IScriptNpc
{
    public ulong Guid { get; init; }
    public string? Name { get; set; }
    public ILogger Logger { get; }

    void Say(string message);
}
