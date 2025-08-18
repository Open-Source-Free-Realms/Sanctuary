using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

using Sanctuary.Core.Configuration;

namespace Sanctuary.Database;

internal class DatabaseFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
                .AddUserSecrets<DatabaseContext>()
                .Build();

        var databaseOptions = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>();

        ArgumentNullException.ThrowIfNull(databaseOptions);

        var builder = new DbContextOptionsBuilder();

        CreateInstance(builder, databaseOptions);

        return new DatabaseContext(builder.Options);
    }

    public static DbContextOptionsBuilder CreateInstance(DbContextOptionsBuilder builder, DatabaseOptions databaseOptions)
    {
        var providerAssembly = $"{typeof(DatabaseFactory).Namespace}.{databaseOptions.Provider}";

        switch (databaseOptions.Provider)
        {
            case DatabaseProvider.MySql:
                builder.UseMySql(databaseOptions.ConnectionString, ServerVersion.Parse(databaseOptions.VersionString),
                    x => x.EnableRetryOnFailure().MigrationsAssembly(providerAssembly));
                break;

            case DatabaseProvider.Sqlite:
                builder.UseSqlite(databaseOptions.ConnectionString,
                    x => x.MigrationsAssembly(providerAssembly));
                break;

            default:
                throw new NotImplementedException($"Database provider \"{databaseOptions.Provider}\" isn't implemented.");
        }

        return builder;
    }
}