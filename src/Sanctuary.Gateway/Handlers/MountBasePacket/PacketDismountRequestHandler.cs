using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketDismountRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketDismountRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(PacketDismountRequest));

        if (connection.Player.Mount is null)
            return true;

        var packetDismountResponse = new PacketDismountResponse();

        packetDismountResponse.RiderGuid = connection.Player.Guid;
        packetDismountResponse.CompositeEffectId = 0; // PFX_Teleport_Flash

        connection.Player.SendTunneledToVisible(packetDismountResponse, true);

        connection.Player.Mount.Dispose();
        connection.Player.Mount = null;

        connection.Player.UpdateCharacterStats(
            CharacterStats.MaxMovementSpeed.Set(8f),
            CharacterStats.GlideEnabled.Set(0),
            CharacterStats.JumpHeight.Set(0f));

        return true;
    }
}