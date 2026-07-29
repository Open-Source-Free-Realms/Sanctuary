using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Sanctuary.Database.MySql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMySqlDatabase(this IServiceCollection services, DatabaseOptions databaseOptions)
    {
        if (databaseOptions.Provider != DatabaseProvider.MySql)
            throw new InvalidOperationException($"Expected database provider '{DatabaseProvider.MySql}' but found '{databaseOptions.Provider}'.");

        services.AddSingleton(_ =>
        {
            var builder = new DbContextOptionsBuilder<DatabaseContext>();
            MySqlDatabaseFactory.CreateInstance(builder, databaseOptions);
            return (DbContextOptions<DatabaseContext>)builder.Options;
        });

        services.AddSingleton<IDbContextFactory<DatabaseContext>, MySqlDatabaseFactory>();

        return services;
    }
}