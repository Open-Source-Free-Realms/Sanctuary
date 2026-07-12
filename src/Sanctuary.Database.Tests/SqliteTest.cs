using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Database.Sqlite;


namespace Sanctuary.Database.Tests;

[TestClass]
public class SqliteTest
{
    private DatabaseContext _context = null!;

    [TestInitialize]
    public void Setup()
    {
        var databaseOptions = new DatabaseOptions
        {
            Provider = DatabaseProvider.Sqlite,
            ConnectionString = "Data Source=:memory:"
        };

        var builder = new DbContextOptionsBuilder<SqliteDatabaseContext>();

        SqliteDatabaseFactory.CreateInstance(builder, databaseOptions);

        _context = new SqliteDatabaseContext(builder.Options);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Assert.IsTrue(_context.Database.EnsureDeleted());

        _context.Dispose();
    }

    [TestMethod]
    public void IsValid()
    {
        _context.Database.Migrate();

        Assert.IsTrue(_context.Database.CanConnect());
    }
}