using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class ScriptableNpc(IScriptNpc npc) : ILuaUserData
{
    private IScriptNpc _npc => npc;

    public LuaTable? Metatable { get; set; } = SharedMetatable;

    #region API

    private static readonly LuaFunction SayFunction = new("say", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ScriptableNpc>(0);
        var message = context.GetArgument<string>(1);

        self._npc.Say(message);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SayLocalizedFunction = new("sayLocalized", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ScriptableNpc>(0);
        var stringId = context.GetArgument<int>(1);

        self._npc.SayLocalized(stringId);

        return new ValueTask<int>(context.Return());
    });

    #endregion

    private static readonly LuaTable SharedMetatable = BuildMetatable();

    private static LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", static (context, cancellationToken) =>
        {
            // Argument 0 is the NPC userdata being indexed; argument 1 is the key.
            var self = context.GetArgument<ScriptableNpc>(0);
            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "guid" => new LuaValue(self._npc.Guid),
                "name" => new LuaValue(self._npc.Name ?? ""),
                "say" => SayFunction,
                "sayLocalized" => SayLocalizedFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
