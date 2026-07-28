using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

public class ScriptManager : IScriptManager
{
    private static readonly string BaseDirectory = ResolveScriptsDirectory();

    private readonly ILogger _logger;
    private readonly ScriptRuntime _runtime = new();

    private readonly ConcurrentDictionary<IScript, ScriptContext> _contexts = new();

    public ScriptManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ScriptManager>();
    }

    public static string GetScriptsDirectory(string category) => Path.Combine(BaseDirectory, category);

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

    public bool DeleteContext(IScript script)
    {
        return _contexts.TryRemove(script, out _);
    }

    public bool GetOrCreateContext(IScript script, out ScriptContext context)
    {
        if (_contexts.TryGetValue(script, out var existingContext))
        {
            context = existingContext;
            return false;
        }

        var env = CreateEnv();

        ILuaUserData userData = script switch
        {
            IScriptZone zone => ScriptableZone.GetOrCreate(zone),
            IScriptNpc npc => ScriptableNpc.GetOrCreate(npc),
            _ => throw new NotSupportedException($"No scriptable wrapper for {script.GetType().Name}.")
        };

        context = new ScriptContext(_runtime, script.Logger, env, userData);
        _contexts[script] = context;
        return true;
    }
}