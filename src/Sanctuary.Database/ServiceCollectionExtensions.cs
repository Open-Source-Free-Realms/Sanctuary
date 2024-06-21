using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Core.Configuration;

namespace Sanctuary.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseOptions = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>();

        ArgumentNullException.ThrowIfNull(databaseOptions);

        services.AddDbContextFactory<DatabaseContext>(builder => DatabaseFactory.CreateInstance(builder, databaseOptions), ServiceLifetime.Transient);

        return services;
    }
}