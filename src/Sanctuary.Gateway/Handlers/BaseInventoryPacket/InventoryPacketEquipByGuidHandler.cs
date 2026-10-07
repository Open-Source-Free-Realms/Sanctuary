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
public static class InventoryPacketEquipByGuidHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketEquipByGuidHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketEquipByGuid.TryDeserialize(data, out var inventoryPacketEquipByGuid))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketEquipByGuid));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketEquipByGuid), inventoryPacketEquipByGuid);

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == inventoryPacketEquipByGuid.Guid);

        if (clientItem is null)
        {
            _logger.LogWarning("User tried to equip unknown item. {guid}", inventoryPacketEquipByGuid.Guid);
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("User tried to equip unknown item definition. {guid} {definition}", inventoryPacketEquipByGuid.Guid, clientItem.Definition);
            return true;
        }

        var profile = connection.Player.Profiles.SingleOrDefault(x => x.Id == inventoryPacketEquipByGuid.ProfileId);

        if (profile is null)
        {
            _logger.LogWarning("Invalid player profile. {guid} {profile}", inventoryPacketEquipByGuid.Guid, inventoryPacketEquipByGuid.ProfileId);
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbProfile = dbContext.Profiles
            .Include(x => x.Items)
            .SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == inventoryPacketEquipByGuid.ProfileId);

        if (dbProfile is null)
        {
            _logger.LogWarning("Invalid database profile.");
            return true;
        }

        var dbItem = dbContext.Items
            .SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == inventoryPacketEquipByGuid.Guid);

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

        clientUpdatePacketEquipItem.Guid = inventoryPacketEquipByGuid.Guid;

        clientUpdatePacketEquipItem.Attachment.ModelName = clientItemDefinition.ModelName;
        clientUpdatePacketEquipItem.Attachment.TextureAlias = clientItemDefinition.TextureAlias;
        clientUpdatePacketEquipItem.Attachment.TintAlias = clientItemDefinition.TintAlias;
        clientUpdatePacketEquipItem.Attachment.TintId = clientItem.Tint == 0 ? clientItemDefinition.Icon.TintId : clientItem.Tint;
        clientUpdatePacketEquipItem.Attachment.CompositeEffectId = clientItemDefinition.CompositeEffectId;
        clientUpdatePacketEquipItem.Attachment.Slot = inventoryPacketEquipByGuid.Slot;

        clientUpdatePacketEquipItem.ProfileId = inventoryPacketEquipByGuid.ProfileId;

        clientUpdatePacketEquipItem.Equip = true;

        connection.SendTunneled(clientUpdatePacketEquipItem);

        var playerUpdatePacketEquipItemChange = new PlayerUpdatePacketEquipItemChange();

        playerUpdatePacketEquipItemChange.Guid = connection.Player.Guid;

        playerUpdatePacketEquipItemChange.Id = clientItem.Id;

        playerUpdatePacketEquipItemChange.Attachment = clientUpdatePacketEquipItem.Attachment;

        playerUpdatePacketEquipItemChange.ProfileId = connection.Player.ActiveProfileId;

        if (!_resourceManager.ItemClasses.TryGetValue(clientItemDefinition.Class, out var itemClass))
        {
            _logger.LogWarning("User tried to equip unknown item class. {guid} {definition}", inventoryPacketEquipByGuid.Guid, clientItemDefinition.Class);
            return true;
        }

        playerUpdatePacketEquipItemChange.WieldType = itemClass.WieldType;

        if (inventoryPacketEquipByGuid.ProfileId == connection.Player.ActiveProfileId)
            connection.Player.SendTunneledToVisible(playerUpdatePacketEquipItemChange);

        connection.Player.SendToolbar();

        connection.Player.RefreshWeaponFlair(inventoryPacketEquipByGuid.ProfileId, inventoryPacketEquipByGuid.Slot);

        return true;
    }
}
