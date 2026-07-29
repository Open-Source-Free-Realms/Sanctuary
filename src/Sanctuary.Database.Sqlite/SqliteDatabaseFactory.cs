using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Sanctuary.Database.Sqlite;

public sealed class SqliteDatabaseFactory : IDbContextFactory<DatabaseContext>, IDesignTimeDbContextFactory<SqliteDatabaseContext>
{
    private readonly DbContextOptions<DatabaseContext> _options = null!;

    public SqliteDatabaseFactory()
    {
    }

    public SqliteDatabaseFactory(DbContextOptions<DatabaseContext> options)
    {
        _options = options;
    }

    public DatabaseContext CreateDbContext()
    {
        return new SqliteDatabaseContext(_options);
    }

    public SqliteDatabaseContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
                .AddUserSecrets<SqliteDatabaseContext>()
                .Build();

        var databaseOptions = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>();

        ArgumentNullException.ThrowIfNull(databaseOptions);

        var builder = new DbContextOptionsBuilder();

        CreateInstance(builder, databaseOptions);

        return new SqliteDatabaseContext(builder.Options);
    }

    public static DbContextOptionsBuilder CreateInstance(DbContextOptionsBuilder builder, DatabaseOptions databaseOptions)
    {
        return builder.UseSqlite(databaseOptions.ConnectionString);
    }
}