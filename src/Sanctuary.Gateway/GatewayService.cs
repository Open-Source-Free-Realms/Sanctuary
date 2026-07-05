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
using Sanctuary.Gateway.Services;
using Sanctuary.UdpLibrary.Enumerations;
using System.Linq;

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
    private readonly IInteractionManager _interactionManager;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly BanStore _banStore;

    public GatewayService(
        ILogger<GatewayService> logger,
        LoginClient client,
        GatewayServer server,
        IOptions<GatewayServerOptions> options,
        IZoneManager zoneManager,
        IServiceProvider serviceProvider,
        IResourceManager resourceManager,
        IInteractionManager interactionManager,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        BanStore banStore,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _client = client;
        _server = server;
        _options = options.Value;
        _zoneManager = zoneManager;
        _serviceProvider = serviceProvider;
        _resourceManager = resourceManager;
        _interactionManager = interactionManager;
        _dbContextFactory = dbContextFactory;
        _banStore = banStore;
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

        ItemActionBarService.ApplyCarouselDefinitionCompatibility(_resourceManager);


        // Load zones.
        if (!_zoneManager.Load())
        {
            _logger.LogCritical("Cannot start {server}, failed to load zones.", nameof(GatewayServer));

            _hostApplicationLifetime.StopApplication();

            return Task.CompletedTask;
        }

        // Load interactions.
        if (!_interactionManager.Load())
        {
            _logger.LogCritical("Cannot start {server}, failed to load interactions.", nameof(GatewayServer));

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

        _logger.LogInformation("{server} started and is listening on port '{port}'.", nameof(GatewayServer), _options.Port);

        _server.OnStarted();

        var nextBanSweepUtc = DateTime.UtcNow.AddSeconds(10);

        // Main server loop.
        while (!cancellationToken.IsCancellationRequested && clientConnection.Status != Status.Disconnected)
        {
            _server.GiveTime();
            _client.GiveTime();

            if (DateTime.UtcNow >= nextBanSweepUtc)
            {
                nextBanSweepUtc = DateTime.UtcNow.AddSeconds(10);
                RunBanSweep();
            }

            Thread.Sleep(1);
        }

        return Task.CompletedTask;
    }

    private void RunBanSweep()
{
    try
    {
        _banStore.ReloadIfChanged();

        var connections = _server.Connections.ToArray();

        foreach (var connection in connections)
        {
            if (connection is null)
                continue;

            if (connection.Status == Status.Disconnected)
                continue;

            if (connection.UserId == 0)
                continue;

            if (_banStore.IsUserIdBanned(connection.UserId))
            {
                _logger.LogInformation(
                    "Disconnecting banned user. UserId: {userId}, Username: {username}, Connection: {connection}",
                    connection.UserId,
                    connection.Username ?? string.Empty,
                    connection);

                connection.Disconnect();
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during ban sweep.");
    }
}
}