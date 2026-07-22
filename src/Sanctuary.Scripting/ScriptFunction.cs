using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

public class ScriptFunction(ScriptContext context, LuaValue function, string functionName)
{
    private readonly ScriptContext _context = context;
    private readonly LuaValue _function = function;
    private readonly string _functionName = functionName;

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

        luaArgs[0] = ToLuaValue(_context._userData);

        for (var i = 0; i < args.Length; i++)
            luaArgs[i + 1] = ToLuaValue(args[i]);

        return await CallAsync(luaArgs);
    }

    private async ValueTask<object?[]?> CallAsync(params LuaValue[] args)
    {
        try
        {
            var results = await _context._state.CallAsync(_function, args);
            return [.. results.Select(FromLuaValue)];
        }
        catch (Exception ex)
        {
            _context._logger.LogError(ex, "Error occurred while calling function '{FunctionName}' in script context.", _functionName);
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