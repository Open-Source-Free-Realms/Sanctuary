using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting;

public interface IScriptable
{
    ILogger Logger { get; }

    ScriptContext GetOrCreateScriptContext();

    bool TryAddScript(string scriptName);

    bool TryRemoveScript(string scriptName);
}