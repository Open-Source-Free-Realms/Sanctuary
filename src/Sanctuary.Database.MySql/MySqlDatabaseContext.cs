using Microsoft.EntityFrameworkCore;

namespace Sanctuary.Database.MySql;

public sealed class MySqlDatabaseContext : DatabaseContext
{
    public MySqlDatabaseContext()
    {
    }

    public MySqlDatabaseContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MySqlDatabaseContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}