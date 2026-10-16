using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PetPerformTrickPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PetPerformTrickPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PetPerformTrickPacket.TryDeserialize(data, out var petPerformTrickPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PetPerformTrickPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PetPerformTrickPacket), petPerformTrickPacket);

        connection.Player.PlayerPet.QueueAction(() => connection.Player.PlayerPet.PerformPetTrick(petPerformTrickPacket.TrickId));

        return true;
    }
}
