using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;
using Lua.Standard;

namespace Sanctuary.Scripting;

public sealed class ScriptRuntime
{
    private readonly LuaState _state;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ScriptRuntime(ILogger logger)
    {
        logger.LogDebug("Initializing scripting...");

        _state = LuaState.Create();

        _state.OpenStandardLibraries();

        logger.LogInformation("Scripting initialized");
    }

    internal LuaTable CreateEnv()
    {
        var env = new LuaTable
        {
            Metatable = new LuaTable()
        };

        env.Metatable["__index"] = _state.Environment; // read access to Lua std lib

        return env;
    }

    public async ValueTask ExecuteFileAsync(string path, LuaTable environment, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var closure = await _state.LoadFileAsync(path, "bt", environment, cancellationToken);
            await _state.ExecuteAsync(closure);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<LuaValue[]> CallAsync(LuaValue function, LuaValue[] args, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await _state.CallAsync(function, args);
        }
        finally
        {
            _gate.Release();
        }
    }
}
