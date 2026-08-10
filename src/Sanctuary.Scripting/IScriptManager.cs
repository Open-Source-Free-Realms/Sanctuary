namespace Sanctuary.Scripting;

public interface IScriptManager
{
    bool Load();

    void Reload();

    bool DeleteContext(IScriptable scriptable);

    bool GetOrCreateContext(IScriptable scriptable, out ScriptContext context);
}