using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

public class ScriptContext
{
    internal readonly ILogger _logger;
    internal readonly LuaTable _environment;
    internal readonly LuaState _state;
    internal readonly ILuaUserData? _userData;

    public ScriptContext(ILogger logger, LuaState state, LuaTable environment, ILuaUserData? userData = null)
    {
        _logger = logger;
        _state = state;
        _environment = environment;
        _userData = userData;

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

    public ScriptFunction? GetFunction(string functionName)
    {
        if (!_environment.TryGetValue(functionName, out var function) || function.Type != LuaValueType.Function)
        {
            return null;
        }

        return new ScriptFunction(this, function, functionName);
    }

    public async ValueTask<object?[]?> CallFunctionAsync(string functionName, params object?[] args)
    {
        var function = GetFunction(functionName);

        if (function is null)
        {
            _logger.LogWarning("Function '{FunctionName}' not found in script context.", functionName);
            return null;
        }

        return await function.CallAsync(args);
    }
}