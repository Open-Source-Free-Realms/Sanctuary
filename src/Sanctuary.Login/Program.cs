using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;

using Sanctuary.Core.Configuration;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Login;
using Sanctuary.Packet.Common.Extensions;
using Sanctuary.UdpLibrary.Configuration;
using Sanctuary.UdpLibrary.Enumerations;

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

    configurationBuilder.AddJsonFile("login.json", optional: false, reloadOnChange: true);

    configurationBuilder.AddEnvironmentVariables();
});

builder.ConfigureServices((hostBuilderContext, serviceCollection) =>
{
    // Options
    serviceCollection.AddOptions<DatabaseOptions>()
        .BindConfiguration(DatabaseOptions.Section)
        .ValidateOnStart();

    serviceCollection.AddOptions<LoginServerOptions>()
        .BindConfiguration(ServerOptions.Section)
        .ValidateOnStart();

    // Database
    serviceCollection.AddDatabase(hostBuilderContext.Configuration);

    // Server Options
    var serverOptions = hostBuilderContext.Configuration.GetSection(ServerOptions.Section).Get<LoginServerOptions>();

    ArgumentNullException.ThrowIfNull(serverOptions);

    // LoginGateway UDP Server
    serviceCollection.AddSingleton(serviceProvider =>
    {
        var udpParams = new UdpParams(ManagerRole.ExternalServer)
        {
            NoDataTimeout = 5000,
            KeepAliveDelay = 2000,
            ProtocolName = "LoginGateway",
            Port = serverOptions.LoginGatewayPort,
        };

        return ActivatorUtilities.CreateInstance<GatewayServer>(serviceProvider, udpParams);
    });

    // Login UDP Server
    serviceCollection.AddSingleton(serviceProvider =>
    {
        var udpParams = new UdpParams
        {
            ProtocolName = "LoginUdp_6",
            Port = serverOptions.Port,
            KeepAliveDelay = 29000,
            CrcBytes = 2
        };

        if (serverOptions.UseCompression)
        {
            udpParams.EncryptMethod[0] = EncryptMethod.UserSupplied;
            udpParams.UserSuppliedEncryptExpansionBytes = 1;
        }

        return ActivatorUtilities.CreateInstance<LoginServer>(serviceProvider, udpParams);
    });

    serviceCollection.AddHostedService<LoginService>();

    // Managers
    serviceCollection.AddSingleton<IResourceManager, ResourceManager>();
});

builder.ConfigureLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.SetMinimumLevel(LogLevel.Debug);

    loggingBuilder.AddNLog();
});

var host = builder.Build();

// Packet Handlers
host.Services.ConfigurePacketHandlers();

await host.RunAsync();