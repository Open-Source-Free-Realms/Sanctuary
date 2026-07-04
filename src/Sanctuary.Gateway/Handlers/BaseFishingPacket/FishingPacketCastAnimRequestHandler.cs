using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FishingPacketCastAnimRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(FishingPacketCastAnimRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FishingPacketCastAnimRequest.TryDeserialize(data, out var pkt))
        {
            _logger.LogError("Failed deserialize CastAnimRequest: {data}", Convert.ToHexString(data));
            return false;
        }
        _logger.LogInformation("Player {g} cast anim at {p}", pkt.Guid, pkt.Position);

        // sendToSelf: the client (case 5) puts the fishing player into the cast pose (state 2 ->
        // sub_CCFFB0 -> sub_95DAE0 face + sub_963DA0 "fishing" bit), which is what makes our OWN
        // proxied character attach its fishing rig (attachment group 7). Without this, our proxied
        // char only enters a fishing state at the bite, so the rod->bobber line (sub_CD0150, which
        // anchors to group 7's EMITTER2 socket) never builds until then. Relaying to visible players
        // already gives THEM the rig for our proxied char; we need it for ourselves too.
        connection.Player.SendTunneledToVisible(pkt, sendToSelf: true);
        return true;
    }
}
