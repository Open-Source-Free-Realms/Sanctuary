using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketEquipByItemRecordHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketEquipByItemRecordHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketEquipByItemRecord.TryDeserialize(data, out var inventoryPacketEquipByItemRecord))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketEquipByItemRecord));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketEquipByItemRecord), inventoryPacketEquipByItemRecord);

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Definition == inventoryPacketEquipByItemRecord.ItemRecord.Definition && x.Tint == inventoryPacketEquipByItemRecord.ItemRecord.Tint);

        if (clientItem is null)
        {
            _logger.LogWarning("User tried to equip unknown item. {definition} {tint}", inventoryPacketEquipByItemRecord.ItemRecord.Definition, inventoryPacketEquipByItemRecord.ItemRecord.Tint);
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("User tried to equip unknown item definition. {id} {definition}", clientItem.Id, clientItem.Definition);
            return true;
        }

        // TODO: Handle other types.
        if (clientItemDefinition.Type != 1)
            return true;

        var profile = connection.Player.Profiles.SingleOrDefault(x => x.Id == inventoryPacketEquipByItemRecord.ProfileId);

        if (profile is null)
        {
            _logger.LogWarning("Invalid player profile. {id} {profile}", clientItem.Id, inventoryPacketEquipByItemRecord.ProfileId);
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbProfile = dbContext.Profiles
            .Include(x => x.Items)
            .SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == inventoryPacketEquipByItemRecord.ProfileId);

        if (dbProfile is null)
        {
            _logger.LogWarning("Invalid database profile.");
            return true;
        }

        var dbItem = dbContext.Items
            .SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == clientItem.Id);

        if (dbItem is null)
        {
            _logger.LogWarning("Invalid database item.");
            return true;
        }

        if (!profile.Items.TryGetValue(clientItemDefinition.Slot, out var profileItem))
        {
            profileItem = new ProfileItem();

            profileItem.Id = clientItem.Id;
            profileItem.Slot = clientItemDefinition.Slot;

            profile.Items.Add(profileItem.Slot, profileItem);

            dbProfile.Items.Add(dbItem);
        }
        else
        {
            var dbItemOld = dbContext.Items
                .SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == profileItem.Id);

            if (dbItemOld is null)
            {
                _logger.LogWarning("Invalid database item.");
                return true;
            }

            profileItem.Id = clientItem.Id;
            profileItem.Slot = clientItemDefinition.Slot;

            dbProfile.Items.Add(dbItem);
            dbProfile.Items.Remove(dbItemOld);
        }

        if (dbContext.SaveChanges() <= 0)
        {
            _logger.LogWarning("Failed to save to database.");
            return true;
        }

        var clientUpdatePacketEquipItem = new ClientUpdatePacketEquipItem();

        clientUpdatePacketEquipItem.Guid = clientItem.Id;

        clientUpdatePacketEquipItem.Attachment.ModelName = clientItemDefinition.ModelName;
        clientUpdatePacketEquipItem.Attachment.TextureAlias = clientItemDefinition.TextureAlias;
        clientUpdatePacketEquipItem.Attachment.TintAlias = clientItemDefinition.TintAlias;
        clientUpdatePacketEquipItem.Attachment.TintId = clientItem.Tint == 0 ? clientItemDefinition.Icon.TintId : clientItem.Tint;
        clientUpdatePacketEquipItem.Attachment.CompositeEffectId = clientItemDefinition.CompositeEffectId;
        clientUpdatePacketEquipItem.Attachment.Slot = inventoryPacketEquipByItemRecord.Slot;

        clientUpdatePacketEquipItem.ProfileId = inventoryPacketEquipByItemRecord.ProfileId;

        clientUpdatePacketEquipItem.Equip = true;

        connection.SendTunneled(clientUpdatePacketEquipItem);

        var playerUpdatePacketEquipItemChange = new PlayerUpdatePacketEquipItemChange();

        playerUpdatePacketEquipItemChange.Guid = connection.Player.Guid;

        playerUpdatePacketEquipItemChange.Id = clientItem.Id;

        playerUpdatePacketEquipItemChange.Attachment = clientUpdatePacketEquipItem.Attachment;

        playerUpdatePacketEquipItemChange.ProfileId = connection.Player.ActiveProfileId;

        if (!_resourceManager.ItemClasses.TryGetValue(clientItemDefinition.Class, out var itemClass))
        {
            _logger.LogWarning("User tried to equip unknown item class. {id} {definition}", clientItem.Id, clientItemDefinition.Class);
            return true;
        }

        playerUpdatePacketEquipItemChange.WieldType = itemClass.WieldType;

        if (inventoryPacketEquipByItemRecord.ProfileId == connection.Player.ActiveProfileId)
            connection.Player.SendTunneledToVisible(playerUpdatePacketEquipItemChange);

        connection.Player.SendToolbar();

        connection.Player.RefreshWeaponFlair(inventoryPacketEquipByItemRecord.ProfileId, inventoryPacketEquipByItemRecord.Slot);

        return true;
    }
}