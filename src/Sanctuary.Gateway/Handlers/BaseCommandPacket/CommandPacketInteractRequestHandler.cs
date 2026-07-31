using System;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketInteractRequestHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketInteractRequestHandler));
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketInteractRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketInteractRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketInteractRequest), packet);

        if (!connection.Player.Zone.TryGetEntity(packet.Guid, out var entity))
            return true;

        if (entity is CollectionNode collectionNode)
            return HandleCollectionNode(connection, collectionNode);

        entity.OnInteract(connection.Player);
        return true;
    }

    private static bool HandleCollectionNode(GatewayConnection connection, CollectionNode node)
    {
        var playerPosition = connection.Player.Position;
        var nodePosition = node.Position;
        var distanceSquared = Vector3.DistanceSquared(
            new Vector3(playerPosition.X, playerPosition.Y, playerPosition.Z),
            new Vector3(nodePosition.X, nodePosition.Y, nodePosition.Z));

        if (distanceSquared > node.InteractRange * node.InteractRange)
            return true;

        if (!node.TryReserve())
            return true;

        var itemPersisted = false;
        var nodeCompleted = false;

        try
        {
            var roll = Random.Shared.Next(node.TypeDefinition.TotalDropWeight);
            var drop = node.TypeDefinition.SelectDrop(roll);
            var itemDefinitionId = drop.ItemDefinitionId;

            if (!_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition))
            {
                _logger.LogError("Collection node type {type} references unknown item definition {itemDefinitionId}.",
                    node.TypeDefinition.Key, itemDefinitionId);
                node.Release();
                return true;
            }

            var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);

            using var dbContext = _dbContextFactory.CreateDbContext();
            var dbCharacter = dbContext.Characters
                .Include(character => character.Items)
                .SingleOrDefault(character => character.Id == characterId);

            if (dbCharacter is null)
            {
                node.Release();
                return true;
            }

            var dbItem = dbCharacter.Items.SingleOrDefault(item => item.Definition == itemDefinitionId && item.Tint == 0);

            if (dbItem is null)
            {
                dbItem = new DbItem
                {
                    Id = dbCharacter.Items.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
                    Definition = itemDefinitionId,
                    Count = 1,
                    Tint = 0
                };

                dbCharacter.Items.Add(dbItem);
            }
            else
            {
                dbItem.Count++;
            }

            if (dbContext.SaveChanges() <= 0)
            {
                node.Release();
                return true;
            }

            itemPersisted = true;

            var clientItem = connection.Player.Items.SingleOrDefault(item =>
                item.Definition == itemDefinitionId && item.Tint == 0);

            if (clientItem is null)
            {
                clientItem = new ClientItem
                {
                    Id = dbItem.Id,
                    Definition = dbItem.Definition,
                    Count = dbItem.Count,
                    Tint = dbItem.Tint
                };

                connection.Player.Items.Add(clientItem);

                using var writer = new PacketWriter();
                clientItem.Serialize(writer);
                itemDefinition.Serialize(writer);

                connection.SendTunneled(new ClientUpdatePacketItemAdd { Payload = writer.Buffer });
            }
            else
            {
                clientItem.Count = dbItem.Count;
                connection.SendTunneled(new ClientUpdatePacketItemUpdate
                {
                    ItemGuid = clientItem.Id,
                    Count = clientItem.Count
                });
            }

            node.CompleteCollection();
            nodeCompleted = true;

            // The delta packet is not fully decoded. The authoritative self packet is
            // capture-validated and refreshes an already-open collection panel.
            connection.SendSelfToClient();
            SendCollectionRewardToast(connection, clientItem, itemDefinition, node);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect node {nodeId} ({type}).", node.SpawnDefinition.Id, node.TypeDefinition.Key);

            if (itemPersisted && !nodeCompleted)
                node.CompleteCollection();
            else if (!itemPersisted)
                node.Release();

            return false;
        }
    }

    private static void SendCollectionRewardToast(GatewayConnection connection, ClientItem clientItem,
        ClientItemDefinition itemDefinition, CollectionNode node)
    {
        var notificationId = Environment.TickCount & int.MaxValue;

        connection.SendTunneled(new RewardBundlePacket
        {
            SourceGuid = node.Guid ^ (uint)notificationId,
            PlayerGuid = connection.Player.Guid,
            IconId = itemDefinition.Icon.Id,
            NameId = itemDefinition.NameId,
            Quantity = 1,
            EntryIconId = itemDefinition.Icon.Id,
            EntryNameId = itemDefinition.NameId,
            EntryQuantity = 1,
            ItemDefinitionId = clientItem.Definition,
            Tint = clientItem.Tint,
            ItemGuid = notificationId,
            EntryUnknown5 = itemDefinition.DescriptionId
        });
    }
}