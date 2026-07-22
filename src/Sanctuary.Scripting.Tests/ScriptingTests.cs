using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Scripting.Tests;

[TestClass]
public class ScriptingTests
{
    private ServiceProvider _serviceProvider = null!;
    private ScriptManager _scriptManager = null!;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ScriptManager>();

        _serviceProvider = services.BuildServiceProvider();

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
        var zoneScriptsDirectory = ScriptManager.ZoneScriptsDirectory;
        var luaFiles = Directory.GetFiles(zoneScriptsDirectory, "*.lua");
        foreach (var luaFile in luaFiles)
        {
            _ = await _scriptManager.LoadInstanceAsync(luaFile);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }
}

