using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketPreviewStyleCardHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketPreviewStyleCardHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketPreviewStyleCard.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketPreviewStyleCard));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketPreviewStyleCard), packet);

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(packet.Id, out var clientItemDefinition))
        {
            _logger.LogWarning("Unknown item definition. {id}", packet.Id);
            return true;
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
            return true;
        }

        var playerUpdatePacketCustomizationChange = new PlayerUpdatePacketCustomizationChange();

        playerUpdatePacketCustomizationChange.Guid = connection.Player.Guid;

        playerUpdatePacketCustomizationChange.Preview = true;

        playerUpdatePacketCustomizationChange.Customizations.Add(new PlayerCustomizationData
        {
            Id = clientItemDefinition.Param1,
            Param = clientItemDefinition.Param2,
            StringParam = stringParam,
            ItemId = packet.Id
        });

        connection.SendTunneled(playerUpdatePacketCustomizationChange);

        return true;
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
        if (modelCustomizationId == 0)
            return string.Empty;

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