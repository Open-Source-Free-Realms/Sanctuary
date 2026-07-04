using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FishingPacketReelInRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(FishingPacketReelInRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FishingPacketReelInRequest.TryDeserialize(data, out var pkt))
        {
            _logger.LogError("Failed deserialize ReelInRequest: {data}", Convert.ToHexString(data));
            return false;
        }
        _logger.LogInformation("Player {g} reel-in flag={f}", pkt.Guid, pkt.Flag);

        // Resolve the reel-in against the session state (catch if hooked, otherwise a miss).
        var session = Fishing.FishingSessions.GetOrCreate(connection.Player);
        session.OnReel();

        return true;
    }
}
