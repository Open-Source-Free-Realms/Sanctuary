using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PetTactileNotifyHandler
{
    private static ILogger _logger = null!;

    private const int PetTactilePettingMode = 0;
    private const int PetTactileGroomingMode = 2;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PetTactileNotifyHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PetTactileNotify.TryDeserialize(data, out var petTactileNotify))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PetTactileNotify));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PetTactileNotify), petTactileNotify);

        var playerPet = connection.Player.PlayerPet;

        if (petTactileNotify.Mode == PetTactilePettingMode)
            playerPet.QueueAction(() => playerPet.ScratchPet(petTactileNotify.InputMovementSquaredSum, petTactileNotify.InputDurationMs));
        else if (petTactileNotify.Mode == PetTactileGroomingMode)
            playerPet.QueueAction(() => playerPet.GroomPet(petTactileNotify.InputMovementSquaredSum));

        return true;
    }
}
