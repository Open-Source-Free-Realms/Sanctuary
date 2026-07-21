using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sanctuary.Database.Tests;

[TestClass]
public class MySqlTest : DatabaseTestBase
{
    public TestContext TestContext { get; set; }

    protected override void Configure(IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "MySql",
            ["Database:ConnectionString"] = "server=127.0.0.1;port=3306;uid=user;pwd=password;database=sanctuary_test",
            ["Database:VersionString"] = "11.6.0-MariaDB"
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