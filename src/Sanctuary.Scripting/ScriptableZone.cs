using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class ScriptableZone : ILuaUserData
{
    private readonly IScriptZone _zone;
    private LuaTable? _metatable;

    public ScriptableZone(IScriptZone zone)
    {
        _zone = zone;
    }

    public LuaTable? Metatable
    {
        get => _metatable ??= BuildMetatable();
        set => _metatable = value;
    }

    private LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", (context, cancellationToken) =>
        {
            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "id" => new LuaValue(_zone.Id),
                "name" => new LuaValue(_zone.Name),
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
