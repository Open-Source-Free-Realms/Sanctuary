using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.Extensions;

namespace Sanctuary.Database.Tests;

public abstract class DatabaseTestBase
{
    private ServiceProvider _serviceProvider = null!;

    protected abstract void Configure(IConfigurationBuilder configurationBuilder);

    [TestInitialize]
    public void Setup()
    {
        var configurationBuilder = new ConfigurationBuilder();

        Configure(configurationBuilder);

        var config = configurationBuilder.Build();

        var services = new ServiceCollection();

        services.AddDatabase(config);

        _serviceProvider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    protected async Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        var dbContextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        return await dbContextFactory.CreateDbContextAsync(cancellationToken);
    }
}

