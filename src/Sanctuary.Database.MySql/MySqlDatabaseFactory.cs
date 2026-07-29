using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Sanctuary.Database.MySql;

public sealed class MySqlDatabaseFactory : IDbContextFactory<DatabaseContext>, IDesignTimeDbContextFactory<MySqlDatabaseContext>
{
    private readonly DbContextOptions<DatabaseContext> _options = null!;

    public MySqlDatabaseFactory()
    {
    }

    public MySqlDatabaseFactory(DbContextOptions<DatabaseContext> options)
    {
        _options = options;
    }

    public DatabaseContext CreateDbContext()
    {
        return new MySqlDatabaseContext(_options);
    }

    public MySqlDatabaseContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
                .AddUserSecrets<MySqlDatabaseContext>()
                .Build();

        var databaseOptions = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>();

        ArgumentNullException.ThrowIfNull(databaseOptions);

        var builder = new DbContextOptionsBuilder();

        CreateInstance(builder, databaseOptions);

        return new MySqlDatabaseContext(builder.Options);
    }

    public static DbContextOptionsBuilder CreateInstance(DbContextOptionsBuilder builder, DatabaseOptions databaseOptions)
    {
        return builder.UseMySql(databaseOptions.ConnectionString,
            ServerVersion.Parse(databaseOptions.VersionString),
            options =>
            {
                options.EnableRetryOnFailure();
            });
    }
}