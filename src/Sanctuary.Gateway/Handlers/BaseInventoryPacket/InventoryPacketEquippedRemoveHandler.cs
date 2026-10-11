using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketEquippedRemoveHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketEquippedRemoveHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketEquippedRemove.TryDeserialize(data, out var inventoryPacketEquippedRemove))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketEquippedRemove));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketEquippedRemove), inventoryPacketEquippedRemove);

        var profile = connection.Player.Profiles.SingleOrDefault(x => x.Id == inventoryPacketEquippedRemove.ProfileId);

        if (profile is null)
        {
            _logger.LogWarning("Invalid player profile id. {id}", inventoryPacketEquippedRemove.ProfileId);
            return true;
        }

        if (!profile.Items.TryGetValue(inventoryPacketEquippedRemove.Slot, out var profileItem))
        {
            _logger.LogWarning("User tried to unequip empty slot. {slot}", inventoryPacketEquippedRemove.Slot);
            return true;
        }

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == profileItem.Id);

        if (clientItem is null)
        {
            _logger.LogWarning("User tried to unequip unknown item. {id}", profileItem.Id);
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("User tried to equip unknown item definition. {id} {definition}", profileItem.Id, clientItem.Definition);
            return true;
        }

        if (!_resourceManager.ItemClasses.TryGetValue(clientItemDefinition.Class, out var itemClass))
        {
            _logger.LogWarning("User tried to equip unknown item class. {id} {definition}", profileItem.Id, clientItemDefinition.Class);
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbProfile = dbContext.Profiles
            .Include(x => x.Items)
            .SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == inventoryPacketEquippedRemove.ProfileId);

        if (dbProfile is null)
        {
            _logger.LogWarning("Invalid database profile.");
            return true;
        }

        var dbItem = dbProfile.Items.SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == profileItem.Id);

        if (dbItem is null)
        {
            _logger.LogWarning("Invalid database item.");
            return true;
        }

        dbProfile.Items.Remove(dbItem);

        if (dbContext.SaveChanges() <= 0)
        {
            _logger.LogWarning("Failed to save to database.");
            return true;
        }

        profile.Items.Remove(inventoryPacketEquippedRemove.Slot);

        var clientUpdatePacketUnequipSlot = new ClientUpdatePacketUnequipSlot();

        clientUpdatePacketUnequipSlot.Slot = inventoryPacketEquippedRemove.Slot;
        clientUpdatePacketUnequipSlot.ProfileId = inventoryPacketEquippedRemove.ProfileId;

        connection.SendTunneled(clientUpdatePacketUnequipSlot);

        var playerUpdatePacketEquipItemChange = new PlayerUpdatePacketEquipItemChange();

        playerUpdatePacketEquipItemChange.Guid = connection.Player.Guid;

        playerUpdatePacketEquipItemChange.Id = clientItem.Id;

        playerUpdatePacketEquipItemChange.Attachment.Slot = inventoryPacketEquippedRemove.Slot;

        playerUpdatePacketEquipItemChange.ProfileId = inventoryPacketEquippedRemove.ProfileId;

        playerUpdatePacketEquipItemChange.WieldType = itemClass.WieldType;

        if (inventoryPacketEquippedRemove.ProfileId == connection.Player.ActiveProfileId)
            connection.Player.SendTunneledToVisible(playerUpdatePacketEquipItemChange);

        connection.Player.SendToolbar();

        connection.Player.RefreshWeaponFlair(inventoryPacketEquippedRemove.ProfileId, inventoryPacketEquippedRemove.Slot);

        return true;
    }
}