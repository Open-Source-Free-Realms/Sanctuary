using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PetInWaterPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PetInWaterPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PetInWaterPacket.TryDeserialize(data, out var petInWaterPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PetInWaterPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PetInWaterPacket), petInWaterPacket);

        connection.Player.PlayerPet.QueueAction(() => connection.Player.PlayerPet.SetPetInWater(petInWaterPacket.InWater));

        return true;
    }
}
