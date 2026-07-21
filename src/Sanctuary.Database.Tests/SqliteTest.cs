using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Database.Tests;

[TestClass]
public class SqliteTest : DatabaseTestBase
{
    public TestContext TestContext { get; set; }

    protected override void Configure(IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = "Data Source=:memory:"
        });
    }

    [TestMethod]
    public async Task IsValidAsync()
    {
        await using var dbContext = await CreateDbContextAsync(TestContext.CancellationToken);

        await dbContext.Database.MigrateAsync(TestContext.CancellationToken);

        Assert.IsTrue(await dbContext.Database.CanConnectAsync(TestContext.CancellationToken));
    }
}