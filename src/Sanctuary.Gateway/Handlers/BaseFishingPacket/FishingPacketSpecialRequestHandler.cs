using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FishingPacketSpecialRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(FishingPacketSpecialRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!FishingPacketSpecialRequest.TryDeserialize(data, out var pkt))
        {
            _logger.LogError("Failed deserialize SpecialRequest: {data}", Convert.ToHexString(data));
            return false;
        }
        _logger.LogInformation("Player {g} special request data={d}", pkt.Guid, pkt.Data);

        connection.SendTunneled(new FishingPacketSpecialResponse
        {
            Guid = pkt.Guid,
            Unknown = 0,
            Flag = true
        });

        return true;
    }
}
