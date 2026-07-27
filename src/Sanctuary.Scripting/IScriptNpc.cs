using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting;

public interface IScriptNpc : IScript
{
    public ulong Guid { get; init; }
    public string? Name { get; set; }
    public ILogger Logger { get; }
    public IScriptZone Zone { get; }

    void Say(string message);
    void SayLocalized(int stringId);
}
