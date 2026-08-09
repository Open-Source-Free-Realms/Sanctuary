using System.Threading;
using System.Threading.Tasks;

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
