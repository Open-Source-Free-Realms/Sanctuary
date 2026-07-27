using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

public class ScriptFunction
{
    private readonly ScriptRuntime _runtime;
    private readonly ILogger _logger;
    private readonly ILuaUserData? _self;
    private readonly LuaTable _environment;
    private readonly string _functionName;

    internal ScriptFunction(ScriptRuntime runtime, ILogger logger, ILuaUserData? self, LuaTable environment, string functionName)
    {
        _runtime = runtime;
        _logger = logger;
        _self = self;
        _environment = environment;
        _functionName = functionName;
    }

    public async ValueTask<object?[]?> CallAsync(params object?[] args)
    {
        // Resolve the function from its environment at call time so scripts can reassign it at runtime.
        if (!TryResolve(out var function))
            return null;

        var luaArgs = new LuaValue[args.Length];

        for (var i = 0; i < args.Length; i++)
            luaArgs[i] = ToLuaValue(args[i]);

        return await CallAsync(function, luaArgs);
    }

    public async ValueTask<object?[]?> CallAsMethodAsync(params object?[] args)
    {
        // Resolve the function from its environment at call time so scripts can reassign it at runtime.
        if (!TryResolve(out var function))
            return null;

        var luaArgs = new LuaValue[args.Length + 1];

        luaArgs[0] = ToLuaValue(_self);

        for (var i = 0; i < args.Length; i++)
            luaArgs[i + 1] = ToLuaValue(args[i]);

        return await CallAsync(function, luaArgs);
    }

    private bool TryResolve(out LuaValue function)
    {
        return _environment.TryGetValue(_functionName, out function) && function.Type == LuaValueType.Function;
    }

    private async ValueTask<object?[]?> CallAsync(LuaValue function, LuaValue[] args)
    {
        try
        {
            var results = await _runtime.CallAsync(function, args);
            return [.. results.Select(FromLuaValue)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while calling function '{FunctionName}' in script context.", _functionName);
            return null;
        }
    }

    private static LuaValue ToLuaValue(object? arg)
    {
        if (arg is null)
            return LuaValue.Nil;

        return LuaValue.FromObject(arg);
    }

    private static object? FromLuaValue(LuaValue value)
    {
        if (value.Type == LuaValueType.Nil)
            return null;

        return value.Read<object>();
    }
}