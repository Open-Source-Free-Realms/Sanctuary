using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Sanctuary.Database.Sqlite;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteDatabase(this IServiceCollection services, DatabaseOptions databaseOptions)
    {
        if (databaseOptions.Provider != DatabaseProvider.Sqlite)
            throw new InvalidOperationException($"Expected database provider '{DatabaseProvider.Sqlite}' but found '{databaseOptions.Provider}'.");

        services.AddSingleton(_ =>
        {
            var builder = new DbContextOptionsBuilder<DatabaseContext>();
            SqliteDatabaseFactory.CreateInstance(builder, databaseOptions);
            return (DbContextOptions<DatabaseContext>)builder.Options;
        });

        services.AddSingleton<IDbContextFactory<DatabaseContext>, SqliteDatabaseFactory>();

        return services;
    }
}