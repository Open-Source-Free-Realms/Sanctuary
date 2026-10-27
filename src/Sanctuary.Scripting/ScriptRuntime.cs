using System;
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

    #region Sandbox

    /// <summary>
    /// Globals GM-authored scripts may use. Everything else (io, os, load, require, debug...) is left out.
    /// </summary>
    private static readonly string[] SandboxGlobals =
    [
        "pairs", "ipairs", "next", "select", "type", "tostring", "tonumber", "pcall", "error", "assert"
    ];

    /// <summary>
    /// Libraries copied per sandbox, so a script can't modify them for everyone else.
    /// </summary>
    private static readonly string[] SandboxLibraries = ["math", "string", "table"];

    /// <summary>
    /// Creates an isolated environment for GM-authored source (see <see cref="ScriptSandbox"/>).
    /// </summary>
    internal LuaTable CreateSandboxEnv()
    {
        var env = new LuaTable();

        foreach (var name in SandboxGlobals)
            env[name] = _state.Environment[name];

        foreach (var name in SandboxLibraries)
        {
            var copy = new LuaTable();

            foreach (var entry in _state.Environment[name].Read<LuaTable>())
                copy[entry.Key] = entry.Value;

            env[name] = copy;
        }

        env["unpack"] = env["table"].Read<LuaTable>()["unpack"];

        return env;
    }

    /// <summary>
    /// Compiles and runs a chunk, cancelling it if it runs longer than <paramref name="timeout"/>.
    /// </summary>
    internal async ValueTask ExecuteSourceAsync(string source, string chunkName, LuaTable environment, TimeSpan timeout)
    {
        await _gate.WaitAsync();
        try
        {
            var closure = _state.Load(source.AsSpan(), chunkName, environment);

            using var cancellationTokenSource = new CancellationTokenSource(timeout);
            await _state.ExecuteAsync(closure, cancellationTokenSource.Token);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Calls a function, cancelling it if it runs longer than <paramref name="timeout"/>. The wait for the
    /// runtime is not counted.
    /// </summary>
    internal async ValueTask CallAsync(LuaValue function, LuaValue[] args, TimeSpan timeout)
    {
        await _gate.WaitAsync();
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(timeout);
            await _state.CallAsync(function, args, cancellationTokenSource.Token);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion
}
