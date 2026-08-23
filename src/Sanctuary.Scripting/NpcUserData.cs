using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Lua;

using Sanctuary.Core.Actions;

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

        // MoveTo just constructs the action now - pair it with SetAction here so the simple
        // "fire and forget" Lua call still starts moving immediately, same as before.
        self._npc.SetAction("move", self._npc.MoveTo(x, y, z, direct));

        return new ValueTask<int>(context.Return());
    });

    // Supported step keys: "say" (InstantAction), "wait" (seconds), "moveTo" (real MoveToAction,
    // reports done only once actually arrived), "parallel"/"sequential" (nested step lists).
    // NOTE: no "anim"/"reward" steps yet - those C# capabilities don't exist on Npc at all yet.
    private static readonly LuaFunction RunBehaviorFunction = new("runBehavior", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<NpcUserData>(0);
        var slot = context.GetArgument<string>(1);
        var step = context.GetArgument<LuaTable>(2);

        var action = ParseStep(self._npc, step);
        self._npc.SetAction(slot, action);

        return new ValueTask<int>(context.Return());
    });

    private static IAction ParseSequence(IScriptableNpc npc, LuaTable steps)
    {
        return new SequentialAction(ParseSteps(npc, steps));
    }

    private static IAction[] ParseSteps(IScriptableNpc npc, LuaTable steps)
    {
        var result = new IAction[steps.ArrayLength];

        for (var i = 0; i < steps.ArrayLength; i++)
            result[i] = ParseStep(npc, steps[(double)(i + 1)].Read<LuaTable>());

        return result;
    }

    private static IAction ParseStep(IScriptableNpc npc, LuaTable step)
    {
        var say = step["say"];
        if (say.Type != LuaValueType.Nil)
            return new InstantAction(() => npc.Say(say.Read<string>()));

        var wait = step["wait"];
        if (wait.Type != LuaValueType.Nil)
            return new WaitAction(wait.Read<double>());

        var moveTo = step["moveTo"];
        if (moveTo.Type == LuaValueType.Table)
        {
            var position = moveTo.Read<LuaTable>();
            var x = position[1.0].Read<float>();
            var y = position[2.0].Read<float>();
            var z = position[3.0].Read<float>();
            var direct = position.ArrayLength >= 4 && position[4.0].Read<bool>();

            return npc.MoveTo(x, y, z, direct);
        }

        var parallel = step["parallel"];
        if (parallel.Type == LuaValueType.Table)
            return new ParallelAction(ParseSteps(npc, parallel.Read<LuaTable>()));

        var sequential = step["sequential"];
        if (sequential.Type == LuaValueType.Table)
            return ParseSequence(npc, sequential.Read<LuaTable>());

        throw new InvalidOperationException("Unrecognized behavior step");
    }

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
                "runBehavior" => RunBehaviorFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
