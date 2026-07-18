using Microsoft.EntityFrameworkCore;

namespace Sanctuary.Database.Sqlite;

public sealed class SqliteDatabaseContext : DatabaseContext
{
    public SqliteDatabaseContext()
    {
    }

    public SqliteDatabaseContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SqliteDatabaseContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}