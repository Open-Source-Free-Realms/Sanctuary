using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Packet.Common.Extensions;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Gateway;

public class GatewayService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly LoginClient _client;
    private readonly GatewayServer _server;
    private readonly IZoneManager _zoneManager;
    private readonly GatewayServerOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly IResourceManager _resourceManager;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public GatewayService(ILogger<GatewayService> logger, LoginClient client, GatewayServer server, IOptions<GatewayServerOptions> options, IZoneManager zoneManager, IResourceManager resourceManager, IServiceProvider serviceProvider, IDbContextFactory<DatabaseContext> dbContextFactory, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _client = client;
        _server = server;
        _options = options.Value;
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _serviceProvider = serviceProvider;
        _dbContextFactory = dbContextFactory;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _server.OnStopping();

        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Check we can connect to the database.
        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.Database.CanConnect())
        {
            _logger.LogCritical("Cannot start {server}, failed to connect to database.", nameof(GatewayServer));

            _hostApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }

        // Load resources.
        if (!_resourceManager.Load())
        {
            _logger.LogCritical("Cannot start {server}, failed to load resources.", nameof(GatewayServer));

            _hostApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }

        // Load zones.
        if (!_zoneManager.LoadZones())
        {
            _logger.LogCritical("Cannot start {server}, failed to load zones.", nameof(GatewayServer));

            _hostApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }

        // Register services on static packet handlers.
        _serviceProvider.ConfigurePacketHandlers();

        // Connect to the Login Server.

        var clientConnection = _client.EstablishConnection(_options.LoginGatewayAddress);

        if (clientConnection is null)
        {
            _logger.LogCritical("Cannot start {client}. Failed to create client connection.", nameof(LoginClient));

            _hostApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }

        _logger.LogInformation($"{nameof(GatewayServer)} started and is listening on port '{_options.Port}'.");

        _server.OnStarted();

        // Main server loop.
        while (!cancellationToken.IsCancellationRequested && clientConnection.Status != Status.Disconnected)
        {
            _server.GiveTime();
            _client.GiveTime();
        }

        return Task.CompletedTask;
    }
}