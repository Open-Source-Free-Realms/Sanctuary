using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class ScriptableZone(IScriptZone zone) : ILuaUserData
{
    private LuaTable? _metatable;

    public LuaTable? Metatable
    {
        get => _metatable ??= BuildMetatable();
        set => _metatable = value;
    }

    private readonly LuaFunction _spawnNpcFunction = new("spawnNpc", (context, cancellationToken) =>
    {
        var npcId = context.GetArgument<int>(0);
        var x = context.GetArgument<float>(1);
        var y = context.GetArgument<float>(2);
        var z = context.GetArgument<float>(3);
        var heading = context.GetArgument<float>(4);

        var success = zone.TrySpawnNpc(npcId, null, x, y, z, heading);

        return new ValueTask<int>(context.Return(success));
    });

    private readonly LuaFunction _spawnNpcWithGuidFunction = new ("spawnNpcWithGuid", (context, cancellationToken) =>
    {
        var npcId = context.GetArgument<int>(0);
        var npcGuid = context.GetArgument<ulong>(1);
        var x = context.GetArgument<float>(2);
        var y = context.GetArgument<float>(3);
        var z = context.GetArgument<float>(4);
        var heading = context.GetArgument<float>(5);

        var success = zone.TrySpawnNpc(npcId, npcGuid, x, y, z, heading);

        return new ValueTask<int>(context.Return(success));
    });

    private LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", (context, cancellationToken) =>
        {
            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "id" => new LuaValue(zone.Id),
                "name" => new LuaValue(zone.Name),
                "spawnNpc" => _spawnNpcFunction,
                "spawnNpcWithGuid" => _spawnNpcWithGuidFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
