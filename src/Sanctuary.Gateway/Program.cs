using System;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NLog.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Gateway;
using Sanctuary.Database;
using Sanctuary.Core.Configuration;
using Sanctuary.UdpLibrary.Enumerations;
using Sanctuary.UdpLibrary.Configuration;

var builder = new HostBuilder();

builder.ConfigureHostConfiguration((configurationBuilder) =>
{
    configurationBuilder.AddEnvironmentVariables("DOTNET_");
});

builder.ConfigureAppConfiguration((hostBuilderContext, configurationBuilder) =>
{
    if (hostBuilderContext.HostingEnvironment.IsDevelopment())
        configurationBuilder.AddUserSecrets<Program>();
    else
        configurationBuilder.AddJsonFile("database.json", optional: true);

    configurationBuilder.AddEnvironmentVariables();

    configurationBuilder.AddJsonFile("gateway.json", optional: false, reloadOnChange: true);
});

builder.ConfigureServices((hostBuilderContext, serviceCollection) =>
{
    // Options
    serviceCollection.AddOptions<DatabaseOptions>()
        .BindConfiguration(DatabaseOptions.Section)
        .ValidateOnStart();

    serviceCollection.AddOptions<GatewayServerOptions>()
        .BindConfiguration(ServerOptions.Section)
        .ValidateOnStart();

    // Database
    serviceCollection.AddDatabase(hostBuilderContext.Configuration);

    // Server Options
    var serverOptions = hostBuilderContext.Configuration.GetSection(ServerOptions.Section).Get<GatewayServerOptions>();

    ArgumentNullException.ThrowIfNull(serverOptions);

    // LoginGateway UDP Client
    serviceCollection.AddSingleton(serviceProvider =>
    {
        var udpParams = new UdpParams(ManagerRole.ExternalClient)
        {
            KeepAliveDelay = 2000,
            ProtocolName = "LoginGateway"
        };

        return ActivatorUtilities.CreateInstance<LoginClient>(serviceProvider, udpParams);
    });

    // Gateway UDP Server
    serviceCollection.AddSingleton(serviceProvider =>
    {
        var udpParams = new UdpParams
        {
            CrcBytes = 2,
            NoDataTimeout = 30000,
            KeepAliveDelay = 29000,
            Port = serverOptions.Port,
            ProtocolName = serverOptions.ProtocolName,
        };

        if (serverOptions.UseCompression)
        {
            udpParams.EncryptMethod[0] = EncryptMethod.UserSupplied;
            udpParams.UserSuppliedEncryptExpansionBytes = 1;
        }

        return ActivatorUtilities.CreateInstance<GatewayServer>(serviceProvider, udpParams);
    });

    serviceCollection.AddHostedService<GatewayService>();

    // Managers
    serviceCollection.AddSingleton<IZoneManager, ZoneManager>();
    serviceCollection.AddSingleton<IResourceManager, ResourceManager>();
});

builder.ConfigureLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.SetMinimumLevel(LogLevel.Debug);

    loggingBuilder.AddNLog();
});

var host = builder.Build();

await host.RunAsync();