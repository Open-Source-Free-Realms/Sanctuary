namespace Sanctuary.Scripting;

public interface IScript
{
    ScriptContext GetOrCreateScriptContext();

    bool TryAddScript(string scriptName);
}