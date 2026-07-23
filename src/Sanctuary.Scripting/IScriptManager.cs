namespace Sanctuary.Scripting;

public interface IScriptManager
{
    bool Load();

    void Reload();

    ScriptContext? GetContextForZone(IScriptZone zone);

    ScriptContext? GetContextForNpc(IScriptNpc npc);
}