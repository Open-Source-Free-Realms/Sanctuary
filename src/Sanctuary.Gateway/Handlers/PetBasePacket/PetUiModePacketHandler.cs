using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PetUiModePacketHandler
{
    private static ILogger _logger = null!;

    private const int PetUiModeInactive = 0;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PetUiModePacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PetUiModePacket.TryDeserialize(data, out var petUiModePacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PetUiModePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PetUiModePacket), petUiModePacket);

        connection.Player.PlayerPet.QueueAction(() => connection.Player.PlayerPet.SetPetUiMode(petUiModePacket.Active != PetUiModeInactive));

        return true;
    }
}
