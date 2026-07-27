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

    private readonly ConcurrentDictionary<IScriptZone, ScriptContext> _zoneContexts = new();
    private readonly ConcurrentDictionary<IScriptNpc, ScriptContext> _npcContexts = new();

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

        _zoneContexts.Clear();
        _npcContexts.Clear();

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

    public bool GetContextForZone(IScriptZone zone, out ScriptContext context)
    {
        if (_zoneContexts.TryGetValue(zone, out var existingContext))
        {
            context = existingContext;
            return false;
        }

        var env = CreateEnv();

        var zoneUserData = ScriptableZone.GetOrCreate(zone);

        context = new ScriptContext(_runtime, zone.Logger, env, zoneUserData);
        _zoneContexts[zone] = context;
        return true;
    }

    public bool GetContextForNpc(IScriptNpc npc, out ScriptContext context)
    {
        if (_npcContexts.TryGetValue(npc, out var existingContext))
        {
            context = existingContext;
            return false;
        }

        var env = CreateEnv();

        var npcUserData = ScriptableNpc.GetOrCreate(npc);

        context = new ScriptContext(_runtime, npc.Logger, env, npcUserData);
        _npcContexts[npc] = context;
        return true;
    }
}