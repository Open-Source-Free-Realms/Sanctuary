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
    private readonly LuaValue _function;
    private readonly string _functionName;

    internal ScriptFunction(ScriptRuntime runtime, ILogger logger, ILuaUserData? self, LuaValue function, string functionName)
    {
        _runtime = runtime;
        _logger = logger;
        _self = self;
        _function = function;
        _functionName = functionName;
    }

    public async ValueTask<object?[]?> CallAsync(params object?[] args)
    {
        var luaArgs = new LuaValue[args.Length];

        for (var i = 0; i < args.Length; i++)
            luaArgs[i] = ToLuaValue(args[i]);

        return await CallAsync(luaArgs);
    }

    public async ValueTask<object?[]?> CallAsMethodAsync(params object?[] args)
    {
        var luaArgs = new LuaValue[args.Length + 1];

        luaArgs[0] = ToLuaValue(_self);

        for (var i = 0; i < args.Length; i++)
            luaArgs[i + 1] = ToLuaValue(args[i]);

        return await CallAsync(luaArgs);
    }

    private async ValueTask<object?[]?> CallAsync(params LuaValue[] args)
    {
        try
        {
            var results = await _runtime.CallAsync(_function, args);
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