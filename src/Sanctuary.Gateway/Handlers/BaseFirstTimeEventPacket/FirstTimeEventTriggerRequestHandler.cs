using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FirstTimeEventTriggerRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FirstTimeEventTriggerRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FirstTimeEventTriggerRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(FirstTimeEventTriggerRequest));
            return false;
        }

        // TODO: Drop to trace once the layout is confirmed against real client traffic.
        _logger.LogInformation("Received {name} from {player}. ( Payload: {payload} )",
            nameof(FirstTimeEventTriggerRequest), connection.Player.Name, Convert.ToHexString(packet.Payload));

        if (packet.Id is not { } id || !_resourceManager.FirstTimeEvents.ContainsKey(id))
        {
            _logger.LogWarning("{name} did not contain a known first-time event id; the assumed layout may be wrong.", nameof(FirstTimeEventTriggerRequest));
            return true;
        }

        TutorialHelper.SetSeen(_dbContextFactory, connection.Player, TutorialSet.FirstTimeEvents, [id], true);

        return true;
    }
}
