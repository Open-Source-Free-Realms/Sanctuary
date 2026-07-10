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

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("User tried to equip unknown item definition. {guid} {definition}", packet.Guid, clientItem.Definition);
            return true;
        }

        var profile = connection.Player.Profiles.SingleOrDefault(x => x.Id == packet.ProfileId);

        if (profile is null)
        {
            _logger.LogWarning("Invalid player profile. {guid} {profile}", packet.Guid, packet.ProfileId);
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbProfile = dbContext.Profiles
            .Include(x => x.Items)
            .SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == packet.ProfileId);

        if (dbProfile is null)
        {
            _logger.LogWarning("Invalid database profile.");
            return true;
        }

        var dbItem = dbContext.Items
            .SingleOrDefault(x => x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) && x.Id == packet.Guid);

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

        clientUpdatePacketEquipItem.Guid = packet.Guid;

        clientUpdatePacketEquipItem.Attachment.ModelName = clientItemDefinition.ModelName;
        clientUpdatePacketEquipItem.Attachment.TextureAlias = clientItemDefinition.TextureAlias;
        clientUpdatePacketEquipItem.Attachment.TintAlias = clientItemDefinition.TintAlias;
        clientUpdatePacketEquipItem.Attachment.TintId = clientItem.Tint == 0 ? clientItemDefinition.Icon.TintId : clientItem.Tint;
        clientUpdatePacketEquipItem.Attachment.CompositeEffectId = clientItemDefinition.CompositeEffectId;
        clientUpdatePacketEquipItem.Attachment.Slot = packet.Slot;

        clientUpdatePacketEquipItem.ProfileId = packet.ProfileId;

        clientUpdatePacketEquipItem.Equip = true;

        connection.SendTunneled(clientUpdatePacketEquipItem);

        var playerUpdatePacketEquipItemChange = new PlayerUpdatePacketEquipItemChange();

        playerUpdatePacketEquipItemChange.Guid = connection.Player.Guid;

        playerUpdatePacketEquipItemChange.Id = clientItem.Id;

        playerUpdatePacketEquipItemChange.Attachment = clientUpdatePacketEquipItem.Attachment;

        playerUpdatePacketEquipItemChange.ProfileId = connection.Player.ActiveProfileId;

        if (!_resourceManager.ItemClasses.TryGetValue(clientItemDefinition.Class, out var itemClass))
        {
            _logger.LogWarning("User tried to equip unknown item class. {guid} {definition}", packet.Guid, clientItemDefinition.Class);
            return true;
        }

        playerUpdatePacketEquipItemChange.WieldType = itemClass.WieldType;

        connection.Player.SendTunneledToVisible(playerUpdatePacketEquipItemChange);

        // Update the Weapon composite effect if we have a Flair Shard equipped.
        if (packet.Slot == 13)
        {
            if (profile.Items.TryGetValue(7, out var weaponProfileItem))
            {
                var weaponClientItem = connection.Player.Items.SingleOrDefault(x => x.Id == weaponProfileItem.Id);

                if (weaponClientItem is not null)
                {
                    playerUpdatePacketEquipItemChange.Id = weaponClientItem.Id;

                    if (!_resourceManager.ClientItemDefinitions.TryGetValue(weaponClientItem.Definition, out var weaponClientItemDefinition))
                        return true;

                    playerUpdatePacketEquipItemChange.Attachment.ModelName = weaponClientItemDefinition.ModelName;
                    playerUpdatePacketEquipItemChange.Attachment.TextureAlias = weaponClientItemDefinition.TextureAlias;
                    playerUpdatePacketEquipItemChange.Attachment.TintAlias = weaponClientItemDefinition.TintAlias;
                    playerUpdatePacketEquipItemChange.Attachment.TintId = weaponClientItem.Tint;

                    playerUpdatePacketEquipItemChange.Attachment.CompositeEffectId = clientItemDefinition.CompositeEffectId > 0
                        ? clientItemDefinition.CompositeEffectId
                        : weaponClientItemDefinition.CompositeEffectId;

                    playerUpdatePacketEquipItemChange.Attachment.Slot = weaponClientItemDefinition.Slot;

                    playerUpdatePacketEquipItemChange.ProfileId = connection.Player.ActiveProfileId;

                    if (!_resourceManager.ItemClasses.TryGetValue(weaponClientItemDefinition.Class, out itemClass))
                        return true;

                    playerUpdatePacketEquipItemChange.WieldType = itemClass.WieldType;

                    connection.Player.SendTunneledToVisible(playerUpdatePacketEquipItemChange, true);
                }
            }
        }
        else if (packet.Slot == 7)
        {
            if (profile.Items.TryGetValue(13, out var flairShardProfileItem))
            {
                var flairShardClientItem = connection.Player.Items.SingleOrDefault(x => x.Id == flairShardProfileItem.Id);

                if (flairShardClientItem is not null)
                {
                    playerUpdatePacketEquipItemChange.Id = clientItem.Id;

                    if (!_resourceManager.ClientItemDefinitions.TryGetValue(flairShardClientItem.Definition, out var flairShardClientItemDefinition))
                        return true;

                    playerUpdatePacketEquipItemChange.Attachment.ModelName = clientItemDefinition.ModelName;
                    playerUpdatePacketEquipItemChange.Attachment.TextureAlias = clientItemDefinition.TextureAlias;
                    playerUpdatePacketEquipItemChange.Attachment.TintAlias = clientItemDefinition.TintAlias;
                    playerUpdatePacketEquipItemChange.Attachment.TintId = clientItem.Tint;

                    playerUpdatePacketEquipItemChange.Attachment.CompositeEffectId = flairShardClientItemDefinition.CompositeEffectId > 0
                        ? flairShardClientItemDefinition.CompositeEffectId
                        : clientItemDefinition.CompositeEffectId;

                    playerUpdatePacketEquipItemChange.Attachment.Slot = clientItemDefinition.Slot;

                    playerUpdatePacketEquipItemChange.ProfileId = connection.Player.ActiveProfileId;

                    if (!_resourceManager.ItemClasses.TryGetValue(clientItemDefinition.Class, out itemClass))
                        return true;

                    playerUpdatePacketEquipItemChange.WieldType = itemClass.WieldType;

                    connection.Player.SendTunneledToVisible(playerUpdatePacketEquipItemChange, true);
                }
            }
        }

        return true;
    }
}
