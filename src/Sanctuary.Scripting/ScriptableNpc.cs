using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class ScriptableNpc(IScriptNpc npc) : ILuaUserData
{
    private LuaTable? _metatable;

    public LuaTable? Metatable
    {
        get => _metatable ??= BuildMetatable();
        set => _metatable = value;
    }

    #region API

    private readonly LuaFunction _sayFunction = new("say", (context, cancellationToken) =>
    {
        var message = context.GetArgument<string>(0);

        npc.Say(message);

        return new ValueTask<int>(context.Return());
    });

    #endregion

    private LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", (context, cancellationToken) =>
        {
            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "guid" => new LuaValue(npc.Guid),
                "name" => new LuaValue(npc.Name ?? ""),
                "say" => _sayFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
