using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PetSummonRecallPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PetSummonRecallPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PetSummonRecallPacket.TryDeserialize(data, out var petSummonRecallPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PetSummonRecallPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PetSummonRecallPacket), petSummonRecallPacket);

        connection.Player.PlayerPet.QueueAction(() =>
        {
            if (petSummonRecallPacket.Summon)
                connection.Player.PlayerPet.SummonPet(petSummonRecallPacket.PetId);
            else
                connection.Player.PlayerPet.RecallPet();
        });

        return true;
    }
}
