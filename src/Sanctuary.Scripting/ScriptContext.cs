using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

public class ScriptContext
{
    private readonly ILogger _logger;
    private readonly LuaState _state;
    private readonly LuaTable _environment;
    private readonly ILuaUserData _zoneUserData;

    public ScriptContext(ILogger logger, LuaState state, LuaTable environment, ILuaUserData zoneUserData)
    {
        _logger = logger;
        _state = state;
        _environment = environment;
        _zoneUserData = zoneUserData;

        // Override `print` to log to our logger instead of stdout.
        _environment["print"] = new LuaFunction("print", (context, cancellationToken) =>
        {
            var arguments = context.Arguments;
            var builder = new StringBuilder();

            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                    builder.Append('\t');

                builder.Append(arguments[i].ToString());
            }

            _logger.LogInformation("[Lua] {Message}", builder.ToString());
            return new ValueTask<int>(0);
        });
    }

    public async ValueTask CallFunctionAsync(string functionName, params object?[] args)
    {
        if (!_environment.TryGetValue(functionName, out var function))
        {
            _logger.LogWarning("Function '{FunctionName}' not found in script context.", functionName);
            return;
        }

        var luaArgs = new LuaValue[args.Length];

        for (var i = 0; i < args.Length; i++)
            luaArgs[i] = ToLuaValue(args[i]);

        try
        {
            await _state.CallAsync(function, luaArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while calling function '{FunctionName}' in script context.", functionName);
        }
    }

    private LuaValue ToLuaValue(object? arg) => arg switch
    {
        null => LuaValue.Nil,
        LuaValue value => value,
        IScriptZone => new LuaValue(_zoneUserData),
        ILuaUserData userData => new LuaValue(userData),
        string s => s,
        bool b => b,
        int i => i,
        long l => l,
        float f => f,
        double d => d,
        _ => throw new ArgumentException($"Unsupported script argument type '{arg.GetType()}'.", nameof(arg))
    };
}