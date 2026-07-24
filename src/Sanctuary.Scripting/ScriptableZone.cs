using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class ScriptableZone(IScriptZone zone) : ILuaUserData
{
    private IScriptZone _zone => zone;

    public LuaTable? Metatable { get; set; } = SharedMetatable;

    // We weakly cache the wrappers so that we don't have to manually evict them when zones are unloaded.
    private static readonly ConditionalWeakTable<IScriptZone, ScriptableZone> Cache = new();
    public static ScriptableZone GetOrCreate(IScriptZone zone)
        => Cache.GetValue(zone, static z => new ScriptableZone(z));

    private static readonly LuaFunction SpawnNpcFunction = new("spawnNpc", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ScriptableZone>(0);

        var npcId = context.GetArgument<int>(1);
        var x = context.GetArgument<float>(2);
        var y = context.GetArgument<float>(3);
        var z = context.GetArgument<float>(4);
        var heading = context.GetArgument<float>(5);

        if (!self._zone.TrySpawnNpc(npcId, null, x, y, z, heading, out var npc))
        {
            return new ValueTask<int>(context.Return(LuaValue.Nil));
        }

        var userData = ScriptableNpc.GetOrCreate(npc);

        var handle = new LuaValue(userData);

        return new ValueTask<int>(context.Return(handle));
    });

    private static readonly LuaFunction SpawnNpcWithGuidFunction = new("spawnNpcWithGuid", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ScriptableZone>(0);

        var npcId = context.GetArgument<int>(1);
        var npcGuid = context.GetArgument<ulong>(2);
        var x = context.GetArgument<float>(3);
        var y = context.GetArgument<float>(4);
        var z = context.GetArgument<float>(5);
        var heading = context.GetArgument<float>(6);

        if (!self._zone.TrySpawnNpc(npcId, npcGuid, x, y, z, heading, out var npc))
        {
            return new ValueTask<int>(context.Return(LuaValue.Nil));
        }

        var userData = ScriptableNpc.GetOrCreate(npc);

        var handle = new LuaValue(userData);

        return new ValueTask<int>(context.Return(handle));
    });

    private static readonly LuaTable SharedMetatable = BuildMetatable();

    private static LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", static (context, cancellationToken) =>
        {
            var self = context.GetArgument<ScriptableZone>(0);

            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "id" => new LuaValue(self._zone.Id),
                "name" => new LuaValue(self._zone.Name),
                "spawnNpc" => SpawnNpcFunction,
                "spawnNpcWithGuid" => SpawnNpcWithGuidFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
