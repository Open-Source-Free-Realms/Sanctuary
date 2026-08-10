using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

using Sanctuary.Core.Collections;

namespace Sanctuary.Scripting;

public class ScriptContext
{
    private readonly ScriptRuntime _runtime;
    private readonly ILogger _logger;
    private readonly LuaTable _rootEnvironment;
    private readonly LuaValue[] _eventArguments;
    private readonly ConcurrentDictionary<string, LuaTable> _scriptEnvironments = new();
    private readonly ConcurrentDictionary<string, ConcurrentGroupedSet<LuaTable, LuaFunction>> _eventCallbacks = new();

    internal ScriptContext(ScriptRuntime runtime, ILogger logger, ILuaUserData? userData = null)
    {
        _runtime = runtime;
        _logger = logger;
        _rootEnvironment = runtime.CreateEnv();
        _eventArguments = userData is null ? [] : [new LuaValue(userData)];

        // Override `print` to log to our logger instead of stdout.
        _rootEnvironment["print"] = new LuaFunction("print", (context, cancellationToken) =>
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

    public async ValueTask<bool> LoadScriptAsync(string scriptPath)
    {
        var scriptEnv = new LuaTable
        {
            Metatable = new LuaTable()
        };

        // Inherit from the root environment so scripts can share globals and libraries
        scriptEnv.Metatable["__index"] = _rootEnvironment;

        if (!_scriptEnvironments.TryAdd(scriptPath, scriptEnv))
        {
            _logger.LogWarning("Script {Script} was already loaded", scriptPath);
            return false;
        }

        // registerCallback(eventName, callback)
        scriptEnv["registerCallback"] = new LuaFunction("registerCallback", (ctx, cancellationToken) =>
        {
            var eventName = ctx.GetArgument<string>(0);
            var callback = ctx.GetArgument<LuaFunction>(1);

            RegisterCallback(scriptEnv, eventName, callback);

            return new ValueTask<int>(ctx.Return());
        });

        // unregisterCallback(eventName, callback)
        scriptEnv["unregisterCallback"] = new LuaFunction("unregisterCallback", (ctx, cancellationToken) =>
        {
            var eventName = ctx.GetArgument<string>(0);
            var callback = ctx.GetArgument<LuaFunction>(1);

            UnregisterCallback(scriptEnv, eventName, callback);

            return new ValueTask<int>(ctx.Return());
        });

        var scriptFullPath = Path.Combine(ScriptManager.BaseDirectory, scriptPath);

        try
        {
            await _runtime.ExecuteFileAsync(scriptFullPath, scriptEnv);

            _logger.LogDebug("Loaded script {Script}", scriptPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script {Script}", scriptPath);

            // Roll back the reservation and any callbacks registered before the failure.
            UnloadScript(scriptPath);

            return false;
        }
    }

    public bool LoadScript(string scriptName)
    {
        return LoadScriptAsync(scriptName).AsTask().GetAwaiter().GetResult();
    }

    public void LoadScriptInBackground(string scriptName)
    {
        // Fire and forget; safe since LoadScriptAsync does not throw.
        _ = LoadScriptAsync(scriptName);
    }

    public bool UnloadScript(string scriptName)
    {
        if (!_scriptEnvironments.TryRemove(scriptName, out var scriptEnv))
        {
            _logger.LogWarning("Script {Script} was never loaded", scriptName);
            return false;
        }

        foreach (var callbacks in _eventCallbacks.Values)
            callbacks.RemoveGroup(scriptEnv);

        return true;
    }

    internal void RegisterCallback(LuaTable environment, string eventName, LuaFunction callback)
    {
        var callbacks = _eventCallbacks.GetOrAdd(eventName, static _ => new());

        if (!callbacks.TryAdd(environment, callback))
            _logger.LogWarning("Failed to register callback for event {EventName}", eventName);
    }

    internal void UnregisterCallback(LuaTable environment, string eventName, LuaFunction callback)
    {
        if (!_eventCallbacks.TryGetValue(eventName, out var callbacks) ||
            !callbacks.TryRemove(environment, callback))
        {
            _logger.LogWarning("Failed to unregister callback for event {EventName}", eventName);
        }
    }

    public void FireEvent(string eventName)
    {
        if (!_eventCallbacks.TryGetValue(eventName, out var callbacks))
            return;

        foreach (var handler in callbacks.Snapshot)
        {
            // Fire and forget; safe since InvokeCallbackAsync does not throw.
            _ = InvokeCallbackAsync(eventName, handler);
        }
    }

    private async Task InvokeCallbackAsync(string eventName, LuaFunction handler)
    {
        try
        {
            await _runtime.CallAsync(handler, _eventArguments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lua error during callback for event {EventName}", eventName);
        }
    }
}