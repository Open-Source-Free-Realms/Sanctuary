using System;

using Microsoft.Extensions.DependencyInjection;

namespace Sanctuary.Database.MySql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMySqlDatabase(this IServiceCollection services, DatabaseOptions databaseOptions)
    {
        if (databaseOptions.Provider != DatabaseProvider.MySql)
            throw new InvalidOperationException($"Expected database provider '{DatabaseProvider.MySql}' but found '{databaseOptions.Provider}'.");

        services.AddDbContextFactory<DatabaseContext, MySqlDatabaseFactory>(builder =>
            MySqlDatabaseFactory.CreateInstance(builder, databaseOptions), ServiceLifetime.Transient);

        return services;
    }
}