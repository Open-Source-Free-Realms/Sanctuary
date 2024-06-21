using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

using Sanctuary.Game;
using Sanctuary.Database;
using Sanctuary.Core.Configuration;
using Sanctuary.Packet.Common.Extensions;

namespace Sanctuary.Login;

public class LoginService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly LoginServer _loginServer;
    private readonly GatewayServer _gatewayServer;
    private readonly LoginServerOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly IResourceManager _resourceManager;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public LoginService(ILogger<LoginService> logger, IOptions<LoginServerOptions> options, IServiceProvider serviceProvider, IDbContextFactory<DatabaseContext> dbContextFactory, IResourceManager resourceManager, LoginServer loginServer, GatewayServer gatewayServer, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _options = options.Value;
        _loginServer = loginServer;
        _gatewayServer = gatewayServer;
        _serviceProvider = serviceProvider;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Test we can connect to the database.
        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.Database.CanConnect())
        {
            _logger.LogCritical("Cannot start {server}. Failed to reach database.", nameof(LoginService));

            _hostApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }

        // Load resources.
        if (!_resourceManager.Load())
        {
            _logger.LogCritical("Cannot start {server}. Failed to load resource(s).", nameof(LoginService));

            _hostApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }

        // Register services on static packet handlers.
        _serviceProvider.ConfigurePacketHandlers();

        _logger.LogInformation($"{nameof(LoginServer)} started and is listening on port '{_options.Port}'.");
        _logger.LogInformation($"{nameof(GatewayServer)} started and is connecting to address '{_options.LoginGatewayPort}'.");

        // Main server loop.
        while (!cancellationToken.IsCancellationRequested)
        {
            _loginServer.GiveTime();
            _gatewayServer.GiveTime();
        }

        return Task.CompletedTask;
    }
}