using System;
using System.IO;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

public class ScriptManager : IScriptManager
{
    internal static readonly string BaseDirectory = ResolveScriptsDirectory();

    private readonly ILogger _logger;
    private readonly ScriptRuntime _runtime = new();

    private readonly ConcurrentDictionary<IScriptable, ScriptContext> _contexts = new();

    public ScriptManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ScriptManager>();
    }

    private static string ResolveScriptsDirectory()
    {
        // Walk up the current working directory.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Scripts");
            if (Directory.Exists(candidate))
                return candidate;
        }

        // Nothing found; default to alongside the binary.
        return Path.Combine(AppContext.BaseDirectory, "Scripts");
    }

    public bool Load()
    {
        _logger.LogDebug("Initializing Lua engine...");

        _runtime.OpenStandardLibraries();

        _logger.LogInformation("Lua engine initialized");

        return true;
    }

    public void Reload()
    {
        _logger.LogDebug("Reloading scripts...");

        _contexts.Clear();

        _logger.LogInformation("Scripts reloaded");
    }

    internal LuaTable CreateEnv()
    {
        var env = new LuaTable
        {
            Metatable = new LuaTable()
        };

        env.Metatable["__index"] = _runtime.Environment; // read access to Lua std lib

        return env;
    }

    public bool DeleteContext(IScriptable scriptable)
    {
        return _contexts.TryRemove(scriptable, out _);
    }

    public bool GetOrCreateContext(IScriptable scriptable, out ScriptContext context)
    {
        if (_contexts.TryGetValue(scriptable, out var existingContext))
        {
            context = existingContext;
            return false;
        }

        var env = CreateEnv();

        ILuaUserData userData = scriptable switch
        {
            IScriptableZone zone => ZoneUserData.GetOrCreate(zone),
            IScriptableNpc npc => NpcUserData.GetOrCreate(npc),
            _ => throw new NotSupportedException($"No script wrapper for {scriptable.GetType().Name}.")
        };

        var newContext = new ScriptContext(_runtime, scriptable.Logger, env, userData);

        // GetOrAdd is atomic, so if another thread is creating a context for the same object
        // at the same time, we'll get the existing one instead of overwriting it.
        context = _contexts.GetOrAdd(scriptable, newContext);

        var fresh = context == newContext;

        return fresh;
    }
}