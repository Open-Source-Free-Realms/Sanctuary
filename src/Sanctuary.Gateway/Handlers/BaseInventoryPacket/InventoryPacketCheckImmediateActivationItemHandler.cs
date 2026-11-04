using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

//This file handles what happens when 'Use' is used. If Use does more than just mystery chests we will have to refactor this a bit

[PacketHandler]
public static class InventoryPacketCheckImmediateActivationItemHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketCheckImmediateActivationItemHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketCheckImmediateActivationItem.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketCheckImmediateActivationItem));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketCheckImmediateActivationItem), packet);

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == packet.ItemGuid);

        if (clientItem is null)
        {
            _logger.LogWarning("Unknown item guid. {guid}", packet.ItemGuid);
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("Unknown item definition. {theitemdefinition}", clientItem.Definition);
            return true;
        }

        _logger.LogInformation("Item requested to be opened. GUID is {guid} and item definition is {theitemdefinition}", packet.ItemGuid, clientItem.Definition);
        //This packet here works for all mystery chests no matter what type :)
        var isChest = clientItemDefinition.Type == ItemDefinitionHelper.MysteryChestItem;
        var hasKey = connection.Player.Items.Any(x => x.Definition == ItemDefinitionHelper.MysteryKeyId);
        var hasRewards = _resourceManager.RewardTables.ContainsKey($"mystery-chest-{clientItemDefinition.Id}");

        if (isChest && !hasKey)
            ChatHelper.SendSystemMessage(connection.Player, "You need a Mystery Key to open this Mystery Chest!");
        else if (isChest && !hasRewards)
        {
            _logger.LogError("Mystery chest {item} has no associated reward table.", clientItemDefinition.Id);
            ChatHelper.SendSystemMessage(connection.Player, "This chest can't be opened right now because its reward table is missing.");
        }

        var mysteryChestFunPacket = new ClientUpdatePacketImmediateActivationCheck
        {
            ItemGuid = packet.ItemGuid,
            CategoryId = 243,
            BundleId = 5021,
            TitleStringId = 436474,
            BodyStringId = 436472,
            Success = isChest && hasKey && hasRewards
        };

        connection.SendTunneled(mysteryChestFunPacket);

        return true;
    }
}