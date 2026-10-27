using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class ZoneUserData(IScriptableZone zone) : ILuaUserData
{
    private IScriptableZone _zone => zone;

    public LuaTable? Metatable { get; set; } = SharedMetatable;

    // We weakly cache the wrappers so that we don't have to manually evict them when zones are unloaded.
    private static readonly ConditionalWeakTable<IScriptableZone, ZoneUserData> Cache = new();
    public static ZoneUserData GetOrCreate(IScriptableZone zone)
        => Cache.GetValue(zone, static z => new ZoneUserData(z));

    private static readonly LuaFunction SpawnNpcFunction = new("spawnNpc", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);

        var npcId = context.GetArgument<int>(1);
        var x = context.GetArgument<float>(2);
        var y = context.GetArgument<float>(3);
        var z = context.GetArgument<float>(4);
        var heading = context.GetArgument<float>(5);
        var pitch = 0f;
        var roll = 0f;

        if (context.ArgumentCount > 7)
        {
            pitch = context.GetArgument<float>(6);
            roll = context.GetArgument<float>(7);
        }

        if (!self._zone.TrySpawnNpc(npcId, null, x, y, z, heading, pitch, roll, out var npc))
        {
            return new ValueTask<int>(context.Return(LuaValue.Nil));
        }

        var userData = NpcUserData.GetOrCreate(npc);

        var handle = new LuaValue(userData);

        return new ValueTask<int>(context.Return(handle));
    });

    private static readonly LuaFunction SpawnNpcWithGuidFunction = new("spawnNpcWithGuid", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);

        var npcId = context.GetArgument<int>(1);
        var npcGuid = context.Arguments[2].TryRead<string>(out var guidText)
            ? ulong.Parse(guidText, NumberStyles.None, CultureInfo.InvariantCulture)
            : context.GetArgument<ulong>(2);
        var x = context.GetArgument<float>(3);
        var y = context.GetArgument<float>(4);
        var z = context.GetArgument<float>(5);
        var heading = context.GetArgument<float>(6);
        var pitch = 0f;
        var roll = 0f;

        if (context.ArgumentCount > 8)
        {
            pitch = context.GetArgument<float>(7);
            roll = context.GetArgument<float>(8);
        }

        if (!self._zone.TrySpawnNpc(npcId, npcGuid, x, y, z, heading, pitch, roll, out var npc))
        {
            return new ValueTask<int>(context.Return(LuaValue.Nil));
        }

        var userData = NpcUserData.GetOrCreate(npc);

        var handle = new LuaValue(userData);

        return new ValueTask<int>(context.Return(handle));
    });

    private static readonly LuaFunction SetClientAreaEnabledFunction = new("setClientAreaEnabled", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var areaId = context.GetArgument<int>(1);
        var enabled = context.GetArgument<bool>(2);

        self._zone.SetClientAreaEnabled(areaId, enabled);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction BroadcastFunction = new("broadcast", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var message = context.GetArgument<string>(1);

        self._zone.Broadcast(message);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction BroadcastNearFunction = new("broadcastNear", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var message = context.GetArgument<string>(1);
        var x = context.GetArgument<float>(2);
        var z = context.GetArgument<float>(3);
        var radius = context.GetArgument<float>(4);

        self._zone.BroadcastNear(message, x, z, radius);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction IsSeasonActiveFunction = new("isSeasonActive", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<ZoneUserData>(0);
        var seasonName = context.GetArgument<string>(1);

        return new ValueTask<int>(context.Return(self._zone.IsSeasonActive(seasonName)));
    });

    private static readonly LuaTable SharedMetatable = BuildMetatable();

    private static LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", static (context, cancellationToken) =>
        {
            var self = context.GetArgument<ZoneUserData>(0);

            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "id" => new LuaValue(self._zone.Id),
                "name" => new LuaValue(self._zone.Name),
                "spawnNpc" => SpawnNpcFunction,
                "spawnNpcWithGuid" => SpawnNpcWithGuidFunction,
                "setClientAreaEnabled" => SetClientAreaEnabledFunction,
                "broadcast" => BroadcastFunction,
                "broadcastNear" => BroadcastNearFunction,
                "isSeasonActive" => IsSeasonActiveFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
