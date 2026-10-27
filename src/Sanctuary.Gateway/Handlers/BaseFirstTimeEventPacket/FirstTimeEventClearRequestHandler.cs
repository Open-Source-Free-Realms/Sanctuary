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
public static class FirstTimeEventClearRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FirstTimeEventClearRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FirstTimeEventClearRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(FirstTimeEventClearRequest));
            return false;
        }

        // TODO: Drop to trace once the layout is confirmed against real client traffic.
        _logger.LogInformation("Received {name} from {player}. ( Payload: {payload} )",
            nameof(FirstTimeEventClearRequest), connection.Player.Name, Convert.ToHexString(packet.Payload));

        // Assumed: an empty body clears every event (e.g. a "reset tips" option), an id clears just that one.
        if (packet.Payload.Length == 0)
        {
            TutorialHelper.Clear(_dbContextFactory, connection.Player, TutorialSet.FirstTimeEvents);
            return true;
        }

        if (packet.Id is not { } id || !_resourceManager.FirstTimeEvents.ContainsKey(id))
        {
            _logger.LogWarning("{name} did not contain a known first-time event id; the assumed layout may be wrong.", nameof(FirstTimeEventClearRequest));
            return true;
        }

        TutorialHelper.SetSeen(_dbContextFactory, connection.Player, TutorialSet.FirstTimeEvents, [id], false);

        return true;
    }
}
