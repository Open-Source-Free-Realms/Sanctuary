using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Scripting.Tests;

[TestClass]
public class ScriptingTests
{
    private ServiceProvider _serviceProvider = null!;
    private ILogger _logger = null!;
    private ScriptManager _scriptManager = null!;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddSingleton<ScriptManager>();

        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Tests");
        _scriptManager = _serviceProvider.GetRequiredService<ScriptManager>();
    }

    [TestMethod]
    public void InitSucceeds()
    {
        _scriptManager.Load();
    }

    [TestMethod]
    public async Task AllZoneScriptsValid()
    {
        var mockZone = new MockScriptZone(_logger);
        _scriptManager.GetContextForZone(mockZone, out var context);

        var zoneScriptsDirectory = ScriptManager.GetScriptsDirectory("Zone");
        var luaFiles = Directory.GetFiles(zoneScriptsDirectory, "*.lua");

        foreach (var luaFile in luaFiles)
        {
            _logger.LogInformation("Loading script: {ScriptFilePath}", luaFile);
            _ = await context!.LoadScriptAsync("Zone", Path.GetFileName(luaFile));
        }
    }

    [TestMethod]
    public async Task AllNpcScriptsValid()
    {
        var mockZone = new MockScriptZone(_logger);
        var mockNpc = new MockScriptNpc(mockZone);
        _scriptManager.GetContextForNpc(mockNpc, out var context);

        var npcScriptsDirectory = ScriptManager.GetScriptsDirectory("Npc");
        var luaFiles = Directory.GetFiles(npcScriptsDirectory, "*.lua");
        foreach (var luaFile in luaFiles)
        {
            _logger.LogInformation("Loading script: {ScriptFilePath}", luaFile);
            _ = await context!.LoadScriptAsync("Npc", Path.GetFileName(luaFile));
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }
}

