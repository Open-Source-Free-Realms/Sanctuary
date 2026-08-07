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

    internal ScriptContext(ScriptRuntime runtime, ILogger logger, LuaTable environment, ILuaUserData? userData = null)
    {
        _runtime = runtime;
        _logger = logger;
        _rootEnvironment = environment;
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

    public async ValueTask<bool> LoadScriptAsync(string scriptRelativePath)
    {
        var scriptEnv = new LuaTable
        {
            Metatable = new LuaTable()
        };

        // Inherit from the root environment so scripts can share globals and libraries
        scriptEnv.Metatable["__index"] = _rootEnvironment;

        if (!_scriptEnvironments.TryAdd(scriptRelativePath, scriptEnv))
        {
            _logger.LogWarning("Script {Script} was already loaded", scriptRelativePath);
            return false;
        }

        // registerCallback(eventName, callback)
        // registerCallback(eventName, priority, callback)
        scriptEnv["registerCallback"] = new LuaFunction("registerCallback", (ctx, cancellationToken) =>
        {
            var eventName = ctx.GetArgument<string>(0);
            var callback = ctx.GetArgument<LuaFunction>(1);

            RegisterCallback(scriptEnv, eventName, callback);

            return new ValueTask<int>(ctx.Return());
        });

        scriptEnv["unregisterCallback"] = new LuaFunction("unregisterCallback", (ctx, cancellationToken) =>
        {
            var eventName = ctx.GetArgument<string>(0);
            var callback = ctx.GetArgument<LuaFunction>(1);

            UnregisterCallback(scriptEnv, eventName, callback);

            return new ValueTask<int>(ctx.Return());
        });

        var scriptFilePath = Path.Combine(ScriptManager.BaseDirectory, scriptRelativePath);

        try
        {
            await _runtime.ExecuteFileAsync(scriptFilePath, scriptEnv);

            _logger.LogDebug("Loaded script {Script}", scriptRelativePath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script {Script}", scriptRelativePath);

            // Roll back the reservation and any handlers registered before the failure.
            UnloadScript(scriptRelativePath);

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

        foreach (var handlers in _eventCallbacks.Values)
            handlers.RemoveGroup(scriptEnv);

        return true;
    }

    internal void RegisterCallback(LuaTable environment, string eventName, LuaFunction callback)
    {
        var handlers = _eventCallbacks.GetOrAdd(eventName, static _ => new());

        if (!handlers.TryAdd(environment, callback))
            _logger.LogWarning("Failed to register event handler for event {EventName}", eventName);
    }

    internal void UnregisterCallback(LuaTable environment, string eventName, LuaFunction callback)
    {
        if (!_eventCallbacks.TryGetValue(eventName, out var handlers) ||
            !handlers.TryRemove(environment, callback))
        {
            _logger.LogWarning("Failed to unregister event handler for event {EventName}", eventName);
        }
    }

    public void FireEvent(string eventName)
    {
        if (!_eventCallbacks.TryGetValue(eventName, out var handlers))
            return;

        foreach (var handler in handlers.Snapshot)
            _ = _runtime.CallAsync(handler, _eventArguments);
    }
}