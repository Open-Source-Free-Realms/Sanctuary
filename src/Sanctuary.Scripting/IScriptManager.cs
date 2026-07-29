namespace Sanctuary.Scripting;

public interface IScriptManager
{
    bool Load();

    ScriptContext? GetContextForZone(IScriptZone zone);
}