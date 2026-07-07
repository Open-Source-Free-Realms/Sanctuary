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
public static class InventoryPacketUseStyleCardHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketUseStyleCardHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketUseStyleCard.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketUseStyleCard));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketUseStyleCard), packet);

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == packet.ItemGuid);

        if (clientItem is null)
        {
            _logger.LogWarning("Unknown item guid. {guid}", packet.ItemGuid);
            return true;
        }

        Equip(connection, clientItem);

        return true;
    }

    public static void Equip(GatewayConnection connection, ClientItem clientItem)
    {
        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("Unknown item definition. {id}", clientItem.Definition);
            return;
        }

        var stringParam = clientItemDefinition.Param1 switch
        {
            0 => GetHeadStringParam(clientItemDefinition.Param2),
            1 => GetSkinToneStringParam(clientItemDefinition.Param2),
            2 => GetHairStringParam(clientItemDefinition.Param2),
            3 => string.Empty, // Hair Color
            4 => string.Empty, // Eye Color
            5 => GetModelCustomizationStringParam(clientItemDefinition.Param2),
            6 => GetFacePaintStringParam(clientItemDefinition.Param2),
            8 => string.Empty, // Model
            _ => null
        };

        if (stringParam is null)
        {
            _logger.LogWarning("Unknown string param. {param1} {param2}", clientItemDefinition.Param1, clientItemDefinition.Param2);
            return;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.FirstOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
        {
            _logger.LogWarning("Invalid database character.");
            return;
        }

        switch (clientItemDefinition.Param1)
        {
            case 0:
                dbCharacter.Head = stringParam;
                dbCharacter.HeadId = clientItemDefinition.Param2;
                break;

            case 1:
                dbCharacter.SkinTone = stringParam;
                dbCharacter.SkinToneId = clientItemDefinition.Param2;
                break;

            case 2:
                dbCharacter.Hair = stringParam;
                dbCharacter.HairId = clientItemDefinition.Param2;
                break;

            case 3:
                dbCharacter.HairColor = clientItemDefinition.Param2;
                break;

            case 4:
                dbCharacter.EyeColor = clientItemDefinition.Param2;
                break;

            case 5:
                dbCharacter.ModelCustomization = stringParam;
                dbCharacter.ModelCustomizationId = clientItemDefinition.Param2;
                break;

            case 6:
                dbCharacter.FacePaint = stringParam;
                dbCharacter.FacePaintId = clientItemDefinition.Param2;
                break;

            case 8:
                dbCharacter.Model = clientItemDefinition.Param2;
                break;
        }

        var dbItem = dbContext.Items.SingleOrDefault(x => x.CharacterId == dbCharacter.Id && x.Id == clientItem.Id);

        if (dbItem is null)
        {
            _logger.LogWarning("Invalid database style card item.");
            return;
        }

        var deleteItem = clientItem.Count == 1;

        if (deleteItem)
            dbContext.Items.Remove(dbItem);
        else
            dbItem.Count -= 1;

        if (dbContext.SaveChanges() <= 0)
        {
            _logger.LogWarning("Failed to save to database.");
            return;
        }

        connection.Player.Head = dbCharacter.Head;
        connection.Player.HeadId = dbCharacter.HeadId;
        connection.Player.SkinTone = dbCharacter.SkinTone;
        connection.Player.SkinToneId = dbCharacter.SkinToneId;
        connection.Player.Hair = dbCharacter.Hair;
        connection.Player.HairId = dbCharacter.HairId;
        connection.Player.HairColor = dbCharacter.HairColor;
        connection.Player.EyeColor = dbCharacter.EyeColor;
        connection.Player.ModelCustomization = dbCharacter.ModelCustomization;
        connection.Player.ModelCustomizationId = dbCharacter.ModelCustomizationId ?? 0;
        connection.Player.FacePaint = dbCharacter.FacePaint;
        connection.Player.FacePaintId = dbCharacter.FacePaintId ?? 0;
        connection.Player.Model = dbCharacter.Model;

        if (deleteItem)
            connection.Player.Items.Remove(clientItem);
        else
            clientItem.Count = dbItem.Count;

        var playerUpdatePacketCustomizationChange = new PlayerUpdatePacketCustomizationChange();

        playerUpdatePacketCustomizationChange.Guid = connection.Player.Guid;

        playerUpdatePacketCustomizationChange.Customizations.Add(new PlayerCustomizationData
        {
            Id = clientItemDefinition.Param1,
            Param = clientItemDefinition.Param2,
            StringParam = stringParam,
            ItemId = clientItem.Id
        });

        connection.Player.SendTunneledToVisible(playerUpdatePacketCustomizationChange, true);

        if (deleteItem)
        {
            var clientUpdatePacketItemDelete = new ClientUpdatePacketItemDelete
            {
                ItemGuid = clientItem.Id
            };

            connection.SendTunneled(clientUpdatePacketItemDelete);
        }
        else
        {
            var clientUpdatePacketItemUpdate = new ClientUpdatePacketItemUpdate
            {
                ItemGuid = clientItem.Id,
                Count = clientItem.Count
            };

            connection.SendTunneled(clientUpdatePacketItemUpdate);
        }
    }

    private static string? GetHeadStringParam(int headId)
    {
        if (!_resourceManager.HeadMappings.TryGetValue(headId, out var head))
        {
            _logger.LogWarning("Unknown head mapping. {id}", headId);
            return null;
        }

        return head;
    }

    private static string? GetSkinToneStringParam(int skinToneId)
    {
        if (!_resourceManager.SkinToneMappings.TryGetValue(skinToneId, out var skinTone))
        {
            _logger.LogWarning("Unknown skin tone mapping. {id}", skinToneId);
            return null;
        }

        return skinTone;
    }

    private static string? GetHairStringParam(int hairId)
    {
        if (!_resourceManager.HairMappings.TryGetValue(hairId, out var hair))
        {
            _logger.LogWarning("Unknown hair mapping. {id}", hairId);
            return null;
        }

        return hair;
    }

    private static string? GetModelCustomizationStringParam(int modelCustomizationId)
    {
        if (!_resourceManager.ModelCustomizationMappings.TryGetValue(modelCustomizationId, out var modelCustomization))
        {
            _logger.LogWarning("Unknown model customization mapping. {id}", modelCustomizationId);
            return null;
        }

        return modelCustomization;
    }

    private static string? GetFacePaintStringParam(int facePaintId)
    {
        if (!_resourceManager.FacePaintMappings.TryGetValue(facePaintId, out var facePaint))
        {
            _logger.LogWarning("Unknown face paint mapping. {id}", facePaintId);
            return null;
        }

        return facePaint;
    }
}