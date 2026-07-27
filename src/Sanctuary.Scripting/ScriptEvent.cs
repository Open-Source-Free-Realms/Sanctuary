using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sanctuary.Scripting;

public class ScriptEvent
{
    private readonly IReadOnlyList<ScriptFunction> _functions;

    internal ScriptEvent(IReadOnlyList<ScriptFunction> functions)
    {
        _functions = functions;
    }

    public bool HasHandlers => _functions.Count > 0;

    public async ValueTask<bool> CallAsync(params object?[] args)
    {
        foreach (var function in _functions)
        {
            var result = await function.CallAsync(args);

            // Lua truthiness: a truthy return short-circuits lower-priority handlers.
            if (IsTruthy(result))
                return true;
        }

        return false;
    }

    public async ValueTask<bool> CallAsMethodAsync(params object?[] args)
    {
        foreach (var function in _functions)
        {
            var result = await function.CallAsMethodAsync(args);

            // Lua truthiness: a truthy return short-circuits lower-priority handlers.
            if (IsTruthy(result))
                return true;
        }

        return false;
    }

    private static bool IsTruthy(object?[]? result)
    {
        if (result is null || result.Length == 0)
            return false;

        var value = result[0];

        if (value is null)
            return false;

        if (value is bool boolean)
            return boolean;

        return true;
    }
}
