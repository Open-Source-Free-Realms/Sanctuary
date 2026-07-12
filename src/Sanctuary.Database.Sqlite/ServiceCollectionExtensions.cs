using System;

using Microsoft.Extensions.DependencyInjection;

namespace Sanctuary.Database.Sqlite;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteDatabase(this IServiceCollection services, DatabaseOptions databaseOptions)
    {
        if (databaseOptions.Provider != DatabaseProvider.Sqlite)
            throw new InvalidOperationException($"Expected database provider '{DatabaseProvider.Sqlite}' but found '{databaseOptions.Provider}'.");

        services.AddDbContextFactory<DatabaseContext, SqliteDatabaseFactory>(builder =>
            SqliteDatabaseFactory.CreateInstance(builder, databaseOptions), ServiceLifetime.Transient);

        return services;
    }
}