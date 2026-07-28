namespace Sanctuary.Scripting;

public interface IScriptManager
{
    bool Load();

    void Reload();

    bool DeleteContext(IScript script);

    bool GetOrCreateContext(IScript script, out ScriptContext context);
}