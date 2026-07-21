using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;
using Lua.Standard;

namespace Sanctuary.Scripting;

public class ScriptManager : IScriptManager
{
    private const string BaseDirectory = "Scripts";

    private static readonly string ZoneScriptsDirectory = Path.Combine(BaseDirectory, "Zone");

    private readonly ILogger _logger;
    private readonly LuaState _luaState;

    public ScriptManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ScriptManager>();
        _luaState = LuaState.Create();
    }

    public bool Load()
    {
        _logger.LogInformation("Initializing Lua engine...");

        _luaState.OpenStandardLibraries();

        return true;
    }

    async ValueTask<LuaTable> LoadIsolatedAsync(string path)
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

    public ScriptContext? GetContextForZone(IScriptZone zone)
    {
        var scriptFilePath = Path.Combine(ZoneScriptsDirectory, $"{zone.Name}.lua");

        if (!File.Exists(scriptFilePath))
        {
            _logger.LogWarning("No script found for zone '{ZoneName}' (looking in '{ScriptFilePath}').", zone.Name, scriptFilePath);
            return null;
        }

        // To avoid bubbling async all the way up the zone construction chain,
        // we just block on the call here. We will still have the actual context methods
        // be async so we can take advantage of async/await in the scripts themselves.
        var env = LoadIsolatedAsync(scriptFilePath).GetAwaiter().GetResult();

        var zoneUserData = new ScriptableZone(zone);

        return new ScriptContext(zone.Logger, _luaState, env, zoneUserData);
    }
}