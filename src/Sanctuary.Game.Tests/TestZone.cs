using System;
using System.IO;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Tests;

public sealed class TestZone : BaseZone
{
    private sealed class TestZoneDefinition : BaseZoneDefinition;

    private TestZone(IServiceProvider serviceProvider)
        : base(new TestZoneDefinition { Name = "Test" }, serviceProvider)
    {
    }

    public static TestZone Create()
    {
        Directory.CreateDirectory(ResourceManager.BaseDirectory);

        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IResourceManager>(new ResourceManager(NullLogger<ResourceManager>.Instance));
        services.AddSingleton<IScriptManager>(new ScriptManager(NullLoggerFactory.Instance));
        services.AddSingleton<IZoneManager, ZoneManager>();

        return new TestZone(services.BuildServiceProvider());
    }
}
