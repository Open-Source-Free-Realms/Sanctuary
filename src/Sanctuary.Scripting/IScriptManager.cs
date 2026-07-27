namespace Sanctuary.Scripting;

public interface IScriptManager
{
    bool Load();

    void Reload();

    bool GetContextForZone(IScriptZone zone, out ScriptContext context);

    bool GetContextForNpc(IScriptNpc npc, out ScriptContext context);
}