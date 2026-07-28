using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class NpcUserData(IScriptableNpc npc) : ILuaUserData
{
    private IScriptableNpc _npc => npc;

    public LuaTable? Metatable { get; set; } = SharedMetatable;

    // We weakly cache the wrappers so that we don't have to manually evict them when NPCs are unloaded.
    private static readonly ConditionalWeakTable<IScriptableNpc, NpcUserData> Cache = new();
    public static NpcUserData GetOrCreate(IScriptableNpc npc)
        => Cache.GetValue(npc, static n => new NpcUserData(n));

    #region API

    private static readonly LuaFunction SayFunction = new("say", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var message = context.GetArgument<string>(1);

        self._npc.Say(message);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SayLocalizedFunction = new("sayLocalized", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
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
            var self = context.GetArgument<NpcUserData>(0);
            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "guid" => new LuaValue(self._npc.Guid),
                "name" => new LuaValue(self._npc.Name ?? ""),
                "zone" => new LuaValue(ZoneUserData.GetOrCreate(self._npc.Zone)),
                "say" => SayFunction,
                "sayLocalized" => SayLocalizedFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
