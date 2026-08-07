using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;
using Lua.Standard;

namespace Sanctuary.Scripting;

internal sealed class ScriptRuntime
{
    private readonly LuaState _state = LuaState.Create();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LuaTable Environment => _state.Environment;

    public void OpenStandardLibraries() => _state.OpenStandardLibraries();

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

    /// <summary>
    /// Executes a Lua function with the specified arguments in a thread-safe manner.
    /// </summary>
    /// <param name="function">Function to call</param>
    /// <param name="args">Arguments to pass to the function</param>
    /// <param name="logger">Optional logger for logging errors</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the function call</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled</exception>
    public async ValueTask<LuaValue[]> CallAsync(LuaValue function, LuaValue[] args, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await _state.CallAsync(function, args);
        }
        catch (LuaRuntimeException ex)
        {
            logger?.LogError(ex, "Lua runtime error: {Message}", ex.Message);
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }
}
