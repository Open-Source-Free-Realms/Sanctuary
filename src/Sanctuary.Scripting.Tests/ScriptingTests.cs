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

        _scriptManager.Load();
    }

    [TestMethod]
    public async Task AllZoneScriptsValid()
    {
        var mockZone = new MockScriptZone(_logger);
        _scriptManager.GetOrCreateContext(mockZone, out var context);

        var zoneScriptsDirectory = Path.Combine(ScriptManager.BaseDirectory, "Zone");
        var luaFiles = Directory.GetFiles(zoneScriptsDirectory, "*.lua");

        var allSucceeded = true;
        foreach (var luaFile in luaFiles)
        {
            _logger.LogInformation("Loading zone script: {ScriptFilePath}", luaFile);
            var succeeded = await context.LoadScriptAsync(luaFile);
            if (!succeeded)
            {
                _logger.LogError("Failed to load zone script: {ScriptFilePath}", luaFile);
                allSucceeded = false;
            }
        }
        Assert.IsTrue(allSucceeded, "One or more zone scripts failed to load.");
    }

    [TestMethod]
    public async Task AllNpcScriptsValid()
    {
        var mockZone = new MockScriptZone(_logger);
        var mockNpc = new MockScriptNpc(mockZone);
        _scriptManager.GetOrCreateContext(mockNpc, out var context);

        var npcScriptsDirectory = Path.Combine(ScriptManager.BaseDirectory, "Npc");
        var luaFiles = Directory.GetFiles(npcScriptsDirectory, "*.lua");

        var allSucceeded = true;
        foreach (var luaFile in luaFiles)
        {
            _logger.LogInformation("Loading NPC script: {ScriptFilePath}", luaFile);
            var succeeded = await context.LoadScriptAsync(luaFile);
            if (!succeeded)
            {
                _logger.LogError("Failed to load NPC script: {ScriptFilePath}", luaFile);
                allSucceeded = false;
            }
        }
        Assert.IsTrue(allSucceeded, "One or more NPC scripts failed to load.");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }
}

