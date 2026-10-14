using System.Collections.Generic;
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

    private static readonly LuaFunction MoveToFunction = new("moveTo", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var x = context.GetArgument<float>(1);
        var y = context.GetArgument<float>(2);
        var z = context.GetArgument<float>(3);
        var direct = false;

        if (context.ArgumentCount > 4)
        {
            direct = context.GetArgument<bool>(4);
        }

        self._npc.MoveTo(x, y, z, direct);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction GetNearbyPlayersFunction = new("getNearbyPlayers", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var radius = context.GetArgument<float>(1);

        return new ValueTask<int>(context.Return(ToGuidTable(self._npc.GetNearbyPlayerGuids(radius))));
    });

    private static readonly LuaFunction GetPlayersOnTopFunction = new("getPlayersOnTop", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var horizontalRadius = context.GetArgument<float>(1);
        var minHeight = context.GetArgument<float>(2);
        var maxHeight = context.GetArgument<float>(3);

        return new ValueTask<int>(context.Return(ToGuidTable(self._npc.GetPlayerGuidsOnTop(horizontalRadius, minHeight, maxHeight))));
    });

    private static readonly LuaFunction SetCollidableFunction = new("setCollidable", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var collidable = context.GetArgument<bool>(1);

        self._npc.SetCollidable(collidable);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SetBaseAnimationFunction = new("setBaseAnimation", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var animationId = context.GetArgument<int>(1);

        self._npc.SetBaseAnimation(animationId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction AddEffectTagFunction = new("addEffectTag", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var compositeEffectId = context.GetArgument<int>(1);

        var tagId = self._npc.AddEffectTag(compositeEffectId);

        return new ValueTask<int>(context.Return(tagId));
    });

    private static readonly LuaFunction RemoveEffectTagFunction = new("removeEffectTag", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var tagId = context.GetArgument<int>(1);

        self._npc.RemoveEffectTag(tagId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SetSpeedFunction = new("setSpeed", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var speed = context.GetArgument<float>(1);

        self._npc.SetSpeed(speed);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction SetHeadingFunction = new("setHeading", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var heading = context.GetArgument<float>(1);

        self._npc.SetHeading(heading);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction PlayEffectFunction = new("playEffect", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var compositeEffectId = context.GetArgument<int>(1);

        self._npc.PlayEffect(compositeEffectId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction RemoveFunction = new("remove", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var compositeEffectId = context.GetArgument<int>(1);

        self._npc.Remove(compositeEffectId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction PlayAnimationFunction = new("playAnimation", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var animationId = context.GetArgument<int>(1);

        self._npc.PlayAnimation(animationId);

        return new ValueTask<int>(context.Return());
    });

    #endregion

    private static LuaTable ToGuidTable(IEnumerable<ulong> guids)
    {
        var guidTable = new LuaTable();
        var index = 1;

        foreach (var guid in guids)
            guidTable[index++] = guid.ToString();

        return guidTable;
    }

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
                "position" => new LuaValue(new LuaTable
                {
                    ["x"] = self._npc.Position.x,
                    ["y"] = self._npc.Position.y,
                    ["z"] = self._npc.Position.z
                }),
                //
                "say" => SayFunction,
                "sayLocalized" => SayLocalizedFunction,
                "moveTo" => MoveToFunction,
                "getNearbyPlayers" => GetNearbyPlayersFunction,
                "playAnimation" => PlayAnimationFunction,
                "getPlayersOnTop" => GetPlayersOnTopFunction,
                "setCollidable" => SetCollidableFunction,
                "setBaseAnimation" => SetBaseAnimationFunction,
                "addEffectTag" => AddEffectTagFunction,
                "removeEffectTag" => RemoveEffectTagFunction,
                "playEffect" => PlayEffectFunction,
                "setHeading" => SetHeadingFunction,
                "setSpeed" => SetSpeedFunction,
                "isMoving" => new LuaValue(self._npc.IsMoving),
                "remove" => RemoveFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
