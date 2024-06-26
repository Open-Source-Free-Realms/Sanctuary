﻿using System;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketEquipByGuidHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketEquipByGuidHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketEquipByGuid.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketEquipByGuid));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketEquipByGuid), packet);

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == packet.Guid);

        if (clientItem is null)
        {
            _logger.LogWarning("User tried to equip unknown item. {guid}", packet.Guid);
            return true;
        }

        if (!_resourceManager.ItemDefinitions.TryGetValue(clientItem.Definition, out var itemDefinition))
        {
            _logger.LogWarning("User tried to equip unknown item definition. {guid} {definition}", packet.Guid, clientItem.Definition);
            return true;
        }

        var profile = connection.Player.Profile;

        if (profile is null)
        {
            _logger.LogWarning("Invalid player profile. {guid} {profile}", packet.Guid, connection.Player.ActiveProfile);
            return true;
        }

        if (!profile.Items.TryGetValue(itemDefinition.Slot, out var profileItem))
            return true;

        if (profileItem is null)
        {
            profileItem = new ProfileItem();

            profileItem.Id = clientItem.Id;
            profileItem.Slot = itemDefinition.Slot;

            profile.Items.Add(profileItem.Slot, profileItem);
        }
        else
        {
            profileItem.Id = clientItem.Id;
            profileItem.Slot = itemDefinition.Slot;
        }

        var clientUpdatePacketEquipItem = new ClientUpdatePacketEquipItem();

        clientUpdatePacketEquipItem.Guid = packet.Guid;

        clientUpdatePacketEquipItem.Attachment.ModelName = itemDefinition.ModelName;
        clientUpdatePacketEquipItem.Attachment.TextureAlias = itemDefinition.TextureAlias;
        clientUpdatePacketEquipItem.Attachment.TintAlias = itemDefinition.TintAlias;
        clientUpdatePacketEquipItem.Attachment.TintId = clientItem.Tint == 0 ? itemDefinition.Icon.TintId : clientItem.Tint;
        clientUpdatePacketEquipItem.Attachment.CompositeEffectId = itemDefinition.CompositeEffectId;
        clientUpdatePacketEquipItem.Attachment.Slot = packet.Slot;

        clientUpdatePacketEquipItem.ProfileId = packet.ProfileId;

        clientUpdatePacketEquipItem.Equip = true;

        connection.SendTunneled(clientUpdatePacketEquipItem);

        var playerUpdatePacketEquipItemChange = new PlayerUpdatePacketEquipItemChange();

        playerUpdatePacketEquipItemChange.Guid = connection.Player.Guid;

        playerUpdatePacketEquipItemChange.Id = clientItem.Id;

        playerUpdatePacketEquipItemChange.Attachment = clientUpdatePacketEquipItem.Attachment;

        playerUpdatePacketEquipItemChange.ProfileId = connection.Player.ActiveProfile;

        if (!_resourceManager.ItemClasses.TryGetValue(itemDefinition.Class, out var itemClass))
        {
            _logger.LogWarning("User tried to equip unknown item class. {guid} {definition}", packet.Guid, itemDefinition.Class);
            return true;
        }

        playerUpdatePacketEquipItemChange.WieldType = itemClass.WieldType;

        connection.Player.SendTunneledToVisible(playerUpdatePacketEquipItemChange);

        return true;
    }
}