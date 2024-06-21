using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketDismountRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketDismountRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketDismountRequest), packet);

        if (connection.Player.Mount is null)
            return true;

        var packetDismountResponse = new PacketDismountResponse();

        packetDismountResponse.RiderGuid = connection.Player.Guid;
        packetDismountResponse.CompositeEffectId = 0; // PFX_Teleport_Flash

        connection.Player.SendTunneledToVisible(packetDismountResponse, true);

        connection.Player.OnEntityRemove(connection.Player.Mount);

        foreach (var visibleEntity in connection.Player.VisibleEntities)
        {
            visibleEntity.Value.OnEntityRemove(connection.Player.Mount);
        }

        connection.Player.Mount = null;

        connection.Player.UpdateCharacterStats(
            CharacterStats.MaxMovementSpeed.Set(8f),
            CharacterStats.GlideEnabled.Set(0),
            CharacterStats.JumpHeight.Set(0f));

        return true;
    }
}