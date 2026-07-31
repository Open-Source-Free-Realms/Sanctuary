using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;
using Lua.Standard;

namespace Sanctuary.Scripting;

public class ScriptManager : IScriptManager
{
    private static readonly string BaseDirectory = ResolveScriptsDirectory();

    internal static readonly string ZoneScriptsDirectory = Path.Combine(BaseDirectory, "Zone");

    private readonly ILogger _logger;
    private readonly LuaState _luaState;

    public ScriptManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ScriptManager>();
        _luaState = LuaState.Create();
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
        _logger.LogInformation("Initializing Lua engine...");

        _luaState.OpenStandardLibraries();

        return true;
    }

    internal async ValueTask<LuaTable> LoadInstanceAsync(string path)
    {
        var env = new LuaTable
        {
            Metatable = new LuaTable()
        };

        env.Metatable["__index"] = _luaState.Environment; // read access to Lua std lib

        var closure = await _luaState.LoadFileAsync(path, "bt", env, CancellationToken.None);
        await _luaState.ExecuteAsync(closure);
        return env;
    }

    public async ValueTask<ScriptContext?> GetContextForZoneAsync(IScriptZone zone)
    {
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

            return new ScriptContext(zone.Logger, _luaState, env, zoneUserData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script for zone '{ZoneName}'", zone.Name);
            return null;
        }
    }

    public ScriptContext? GetContextForZone(IScriptZone zone)
    {
        return GetContextForZoneAsync(zone).AsTask().GetAwaiter().GetResult();
    }
}