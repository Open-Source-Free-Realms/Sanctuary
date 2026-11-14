using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketUpdatePositionHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;
    private static IRewardManager _rewardManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketUpdatePositionHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
        _rewardManager = serviceProvider.GetRequiredService<IRewardManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PlayerUpdatePacketUpdatePosition.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PlayerUpdatePacketUpdatePosition));
            return false;
        }

        // _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketUpdatePosition), packet);

        if (!connection.Player.TryAcceptTeleportSequence(packet.TeleportSequence))
            return true;

        packet.Guid = connection.Player.Guid;

        var wasGliding = (connection.Player.PositionState & Player.GlidingPositionState) != 0;

        connection.Player.PositionState = packet.State;

        if (wasGliding && (packet.State & Player.GlidingPositionState) == 0)
            UseUpParachute(connection.Player);

        connection.Player.UpdatePosition(packet.Position, packet.Rotation);

        _questManager.OnEvent(connection.Player, new PlayerMovedEvent(connection.Player.Position, connection.Player.Zone.DefinitionId));

        connection.Player.RelayPosition(packet);

        return true;
    }

    private static void UseUpParachute(Player player)
    {
        if (player.Mount is not null
            || !player.TryGetEquippedGlideItem(out var slot, out var clientItem, out var itemDefinition)
            || !itemDefinition.SingleUse)
            return;

        _questManager.OnEvent(player, new ItemUsedEvent(clientItem.Definition));

        var wasLast = clientItem.Count <= 1;

        if (_rewardManager.TryTakeItem(player, clientItem) && wasLast)
            player.UnequipSlot(player.ActiveProfile, slot, clientItem.Id);
    }
}