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
    internal static readonly string ZoneScriptsDirectory = Path.Combine(BaseDirectory, "Zone");
    internal static readonly string NpcScriptsDirectory = Path.Combine(BaseDirectory, "Npc");

    private readonly ILogger _logger;
    private readonly ScriptRuntime _runtime = new();

    private readonly ConcurrentDictionary<IScriptZone, ScriptContext> _zoneContexts = new();
    private readonly ConcurrentDictionary<(IScriptNpc, string), ScriptContext> _npcContexts = new();

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

        _zoneContexts.Clear();
        _npcContexts.Clear();

        _logger.LogInformation("Scripts reloaded");
    }

    internal async ValueTask<LuaTable> LoadInstanceAsync(string path)
    {
        var env = new LuaTable
        {
            Metatable = new LuaTable()
        };

        env.Metatable["__index"] = _runtime.Environment; // read access to Lua std lib

        await _runtime.ExecuteFileAsync(path, env);

        return env;
    }

    public async ValueTask<ScriptContext?> GetContextForZoneAsync(IScriptZone zone)
    {
        if (_zoneContexts.TryGetValue(zone, out var existingContext))
        {
            return existingContext;
        }

        var scriptFilePath = Path.Combine(ZoneScriptsDirectory, $"{zone.Name}.lua");

        if (!File.Exists(scriptFilePath))
        {
            _logger.LogWarning("No script found for zone '{ZoneName}' (looking in '{ScriptFilePath}').", zone.Name, scriptFilePath);
            return null;
        }

        try
        {
            var env = await LoadInstanceAsync(scriptFilePath);

            var zoneUserData = new ScriptableZone(zone);

            var context = new ScriptContext(_runtime, zone.Logger, env, zoneUserData);
            _zoneContexts[zone] = context;
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script for zone '{ZoneName}'", zone.Name);
            return null;
        }
    }

    public async ValueTask<ScriptContext?> GetContextForNpcAsync(IScriptNpc npc)
    {
        if (string.IsNullOrWhiteSpace(npc.ScriptName))
        {
            return null;
        }

        var scriptName = npc.ScriptName;

        // Context is keyed by both the NPC *and* the script name, so a change in the script name yields a new context.
        var contextKey = (npc, scriptName);

        if (_npcContexts.TryGetValue(contextKey, out var existingContext))
        {
            return existingContext;
        }

        var scriptFilePath = Path.Combine(NpcScriptsDirectory, $"{scriptName}.lua");

        if (!File.Exists(scriptFilePath))
        {
            _logger.LogWarning("Script '{ScriptName}' for NPC '{NpcName}' not found (looking in '{ScriptFilePath}').", scriptName, npc.Name, scriptFilePath);
            return null;
        }

        try
        {
            var env = await LoadInstanceAsync(scriptFilePath);

            var npcUserData = new ScriptableNpc(npc);

            var context = new ScriptContext(_runtime, npc.Logger, env, npcUserData);
            _npcContexts[contextKey] = context;
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script for NPC '{NpcName}'", npc.Name);
            return null;
        }
    }

    public ScriptContext? GetContextForZone(IScriptZone zone)
    {
        return GetContextForZoneAsync(zone).AsTask().GetAwaiter().GetResult();
    }

    public ScriptContext? GetContextForNpc(IScriptNpc npc)
    {
        return GetContextForNpcAsync(npc).AsTask().GetAwaiter().GetResult();
    }
}