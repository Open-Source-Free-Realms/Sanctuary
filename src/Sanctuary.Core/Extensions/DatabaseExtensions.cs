using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Database;
using Sanctuary.Database.MySql;
using Sanctuary.Database.Sqlite;

namespace Sanctuary.Core.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseOptions = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>();

        ArgumentNullException.ThrowIfNull(databaseOptions);

        return databaseOptions.Provider switch
        {
            DatabaseProvider.MySql => services.AddMySqlDatabase(databaseOptions),
            DatabaseProvider.Sqlite => services.AddSqliteDatabase(databaseOptions),
            _ => throw new InvalidOperationException($"Unsupported database provider: {databaseOptions.Provider}")
        };
    }
}