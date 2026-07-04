using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FishingPacketCastRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(FishingPacketCastRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FishingPacketCastRequest.TryDeserialize(data, out var pkt))
        {
            _logger.LogError("Failed deserialize CastRequest: {data}", Convert.ToHexString(data));
            return false;
        }
        _logger.LogInformation("Player {g} cast at {p} flag={f}", pkt.Guid, pkt.Position, pkt.Flag);

        // Drive the fishing session: spawns the bobber (guid MUST be the player guid so the client
        // resolves it to the local fishing player) and the catchable fish, then times the bite.
        var session = Fishing.FishingSessions.GetOrCreate(connection.Player);
        session.OnCast(pkt.Position);

        return true;
    }
}
