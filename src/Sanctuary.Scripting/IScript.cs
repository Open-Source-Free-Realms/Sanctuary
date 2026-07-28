using Microsoft.Extensions.Logging;

namespace Sanctuary.Scripting;

public interface IScript
{
    ILogger Logger { get; }

    ScriptContext GetOrCreateScriptContext();

    bool TryAddScript(string scriptName);

    bool TryRemoveScript(string scriptName);
}