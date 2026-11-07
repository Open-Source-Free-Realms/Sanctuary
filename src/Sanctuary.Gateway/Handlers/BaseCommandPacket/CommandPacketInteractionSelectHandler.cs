using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketInteractionSelectHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IInteractionManager _interactionManager = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketInteractionSelectHandler));


        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _interactionManager = serviceProvider.GetRequiredService<IInteractionManager>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketInteractionSelect.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketInteractionSelect));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketInteractionSelect), packet);

        if (connection.Player.OpenInteractionMenu is { } menu
            && menu.Guid == packet.Guid
            && menu.Options.TryGetValue(packet.Id, out var action))
        {
            connection.Player.OpenInteractionMenu = null;

            if (!connection.Player.VisibleNpcs.TryGetValue(packet.Guid, out var menuNpc))
                return true;

            if (!InteractionMenuHelper.CanInteract(menuNpc, connection.Player))
                return true;

            action(connection.Player);
            return true;
        }
        if (connection.Player.VisibleNpcs.TryGetValue(packet.Guid, out var miniGameNpc) &&
            MiniGameNpcHelper.TryInteract(connection.Player, miniGameNpc, packet.Id, _resourceManager))
            return true;

        if (!_interactionManager.TryGet(packet.Id, out var interaction))
        {
            _logger.LogError("Invalid interaction. {interaction}", packet.Id);

            return true;
        }

        if (connection.Player.VisiblePlayers.TryGetValue(packet.Guid, out var player))
        {
            interaction.OnInteract(connection.Player, player);
        }
        else if (connection.Player.VisibleNpcs.TryGetValue(packet.Guid, out var npc))
        {
            interaction.OnInteract(connection.Player, npc);
        }
        else
        {
            _logger.LogWarning("Received interaction for unknown entity. {entity}", packet.Guid);

            return true;
        }

        return true;
    }
}