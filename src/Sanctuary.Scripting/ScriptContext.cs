using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

public class ScriptContext
{
    private readonly ScriptRuntime _runtime;
    private readonly ILogger _logger;
    private readonly LuaTable _environment;

    internal ILuaUserData? UserData { get; }

    internal ScriptContext(ScriptRuntime runtime, ILogger logger, LuaTable environment, ILuaUserData? userData = null)
    {
        _runtime = runtime;
        _logger = logger;
        _environment = environment;
        UserData = userData;

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

        return new ScriptFunction(_runtime, _logger, UserData, function, functionName);
    }
}