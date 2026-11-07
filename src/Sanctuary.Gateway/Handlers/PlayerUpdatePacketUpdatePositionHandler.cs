using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketUpdatePositionHandler
{
    private const byte GlidingStateFlag = 0x20;

    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;
    private static IResourceManager _resourceManager = null!;
    private static IRewardManager _rewardManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketUpdatePositionHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
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

        packet.Guid = connection.Player.Guid;

        var wasGliding = (connection.Player.PositionState & GlidingStateFlag) != 0;

        connection.Player.PositionState = packet.State;

        if (wasGliding && (packet.State & GlidingStateFlag) == 0)
            UseUpParachute(connection.Player);

        connection.Player.UpdatePosition(packet.Position, packet.Rotation);

        _questManager.OnEvent(connection.Player, new PlayerMovedEvent(connection.Player.Position, connection.Player.Zone.DefinitionId));

        connection.Player.RelayPosition(packet);

        return true;
    }

    private static void UseUpParachute(Player player)
    {
        if (player.Mount is not null)
            return;

        var profile = player.ActiveProfile;

        foreach (var (slot, profileItem) in profile.Items)
        {
            var clientItem = player.Items.FirstOrDefault(item => item.Id == profileItem.Id);

            if (clientItem is null
                || !_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var itemDefinition)
                || itemDefinition.Stats?.ContainsKey((int)CharacterStatId.GlideEnabled) != true
                || !itemDefinition.SingleUse)
                continue;

            _questManager.OnEvent(player, new ItemUsedEvent(clientItem.Definition));

            var wasLast = clientItem.Count <= 1;

            if (!_rewardManager.TryTakeItems(player, clientItem.Definition, 1) || !wasLast)
                return;

            profile.Items.Remove(slot);

            player.SendTunneled(new ClientUpdatePacketUnequipSlot { Slot = slot, ProfileId = profile.Id });

            var playerUpdatePacketEquipItemChange = new PlayerUpdatePacketEquipItemChange
            {
                Guid = player.Guid,
                Id = clientItem.Id,
                ProfileId = profile.Id,
                WieldType = player.GetWieldType()
            };
            playerUpdatePacketEquipItemChange.Attachment.Slot = slot;
            player.SendTunneledToVisible(playerUpdatePacketEquipItemChange);

            player.RefreshEquipmentGlide();
            return;
        }
    }
}