using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

// TODO: make this agnostic to Use items
// and make chests.cs so that we can all enjoy shared chest logic

[PacketHandler]
public static class InventoryPacketUseImmediateActivationItemHandler
{
    private static bool TakeOne(DatabaseContext dbContext, DbItem dbItem, ClientItem clientItem)

    {
        var deleteItem = clientItem.Count == 1;

        if (deleteItem)
            dbContext.Items.Remove(dbItem);
        else
            dbItem.Count -= 1;

        return deleteItem;
    }
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IRewardManager _rewardManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketUseImmediateActivationItemHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();

        _rewardManager = serviceProvider.GetRequiredService<IRewardManager>();

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

    }
    private static void UpdatePlayer(GatewayConnection connection, ClientItem clientItem, bool delete)
    {
        if (delete)
        {
            connection.Player.Items.Remove(clientItem);
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
                Count = clientItem.Count -= 1
            };

            connection.SendTunneled(clientUpdatePacketItemUpdate);
        }
    }

    private static Vector4 GetPositionAroundPlayer(Player player, float radius)
    {
        var angle = Random.Shared.NextSingle() * MathF.Tau;

        return new Vector4(
            player.Position.X + MathF.Cos(angle) * radius,
            player.Position.Y,
            player.Position.Z + MathF.Sin(angle) * radius,
            player.Position.W);
    }
    private static Npc? SpawnNpc(Player player, Vector4 position, Quaternion rotation, Action<Npc> configure)
    {
        if (!player.Zone.TryCreateNpc(out var npc))
            return null;

        configure(npc);

        npc.Visible = true;
        npc.UpdatePosition(position, rotation);

        return npc;
    }
    private static readonly Dictionary<int, int> ChestModels = new()
    {
        [79519] = 876,   // Clockwork Mystery Chest
        [78136] = 1511,   // Mystery Chest
        [79520] = 1512,  // Sci-Fi Mystery Chest
        [79521] = 4135,  // Snow Days Mystery Chest
        [79522] = 4460,  // Wonders Mystery Chest
    };

    private const int ChestOpen = 1111;  // com_cast_01 sg_treasure_chest_open.gr2
    private const int ChestShut = 1112; // com_cast_02 sg_treasure_chest_close.gr2
    private const int ChestStand = 1; // do NOTHING!
    private const int ChestOpenStand = 1301;
    private const int ChestCloseAfterMs = 3000;
    private const int ChestDespawnAfterMs = 5000;

    private static void SpawnOpeningChest(Player player, ClientItemDefinition chestDefinition)
    {
        if (!ChestModels.TryGetValue(chestDefinition.Id, out var modelId))
            return;

        var position = GetPositionAroundPlayer(player, 2.0f);
        var toPlayer = new Vector3(player.Position.X - position.X, 0f, player.Position.Z - position.Z);
        var rotation = toPlayer.ToFlatRotation();

        var chestNpc = SpawnNpc(player, position, rotation, npc =>
        {
            npc.NameId = chestDefinition.NameId;
            npc.ModelId = modelId;
            npc.TextureAlias = "";
            npc.TintAlias = "";
            npc.Scale = 1.0f;
            npc.Animation = 1;
            npc.HideNamePlate = true;
            npc.IsInteractable = false;
        });

        if (chestNpc is null)
            return;

        EntityHelper.SetBaseAnimation(player, chestNpc, ChestOpenStand);
        EntityHelper.SetAnimation(player, chestNpc, ChestOpen);

        var openCompositeEffect = 5597;

        player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = chestNpc.Guid,
            CompositeEffectId = openCompositeEffect,
            Position = chestNpc.Position
        }, true);

        var closeAt = Environment.TickCount64 + ChestCloseAfterMs;
        var despawnAt = Environment.TickCount64 + ChestDespawnAfterMs;
        var hasClosed = false;

        chestNpc.UpdateEverySecondAction = () =>
        {
            var now = Environment.TickCount64;

            if (now >= despawnAt)
            {
                EntityHelper.RemovePlayerGracefully(chestNpc);
                return;
            }

            if (!hasClosed && now >= closeAt)
            {
                EntityHelper.SetBaseAnimation(player, chestNpc, ChestStand);
                EntityHelper.SetAnimation(player, chestNpc, ChestShut);
                hasClosed = true;
            }
        };
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketUseImmediateActivationItem.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketUseImmediateActivationItem));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketUseImmediateActivationItem), packet);

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

        if (clientItemDefinition.Type != 28)
        {
            _logger.LogWarning("Unknown or incorrect Mystery Chest type supplied. {theitemtype}", clientItemDefinition.Type);
            return true;
        }

        // Chest loot is in Rewards.json under "mystery-chest-itemid" example Original Mystery Chest is currently "mystery-chest-78136".
        // For future to add a custom chest, add its item and a reward table with that key, and add it to the dictionary
        //TODO: Does any of this work for an exploration chest? Spooktacular? Snow Days gifts? Are there other 'Use' items? I doubt this is the only one
        //And when Groaxy figures out the animation, we will correct the animation.
        var rewardTableKey = $"mystery-chest-{clientItemDefinition.Id}";
        var clientKey = connection.Player.Items.FirstOrDefault(x => x.Definition == 78137);

        if (clientKey is null)
        {
            _logger.LogWarning("Client did not have a Mystery Key!");
            return true;
        }

        if (!_rewardManager.TryRollRewardUnowned(connection.Player, rewardTableKey, out var drop, out var tint) || drop is null)
        {
            _logger.LogError("Could not roll a reward from {table} for {player}.", rewardTableKey, connection.Player.Name.FullName);
            ChatHelper.SendSystemMessage(connection.Player, "This chest can't be opened right now.");
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.FirstOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
        {
            _logger.LogWarning("Invalid database character.");
            return true;
        }

        var dbChest = dbContext.Items.SingleOrDefault(x => x.CharacterId == dbCharacter.Id && x.Id == clientItem.Id);
        var dbKey = dbContext.Items.SingleOrDefault(x => x.CharacterId == dbCharacter.Id && x.Id == clientKey.Id);

        if (dbChest is null)
        {
            _logger.LogWarning("Invalid database item/chest.");
            return true;
        }
        if (dbKey is null)
        {
            _logger.LogWarning("Invalid database item/key.");
            return true;
        }

        var granted = drop is ItemRewardDropDefinition item
            ? _rewardManager.TryGrantItem(connection.Player, item.ItemDefinitionId, tint, item.Quantity)
            : _rewardManager.TryGrantReward(connection.Player, drop);

        if (!granted)
        {
            _logger.LogError("{player} rolled a reward from {table} but it couldn't be granted.", connection.Player.Name.FullName, rewardTableKey);
            ChatHelper.SendSystemMessage(connection.Player, "This chest can't be opened right now due to an internal error!");
            return true;
        }

        var deleteChest = TakeOne(dbContext, dbChest, clientItem);
        var deleteKey = TakeOne(dbContext, dbKey, clientKey);

        if (dbContext.SaveChanges() <= 0)
        {
            _logger.LogError("{player} was granted a reward but the chest and key were retained.", connection.Player.Name.FullName);
            return true;
        }

        UpdatePlayer(connection, clientItem, deleteChest);
        UpdatePlayer(connection, clientKey, deleteKey);

        SpawnOpeningChest(connection.Player, clientItemDefinition);
        _logger.LogInformation("Item being opened. GUID is {guid} and item definition is {theitemdefinition}, key is {thekey}", packet.ItemGuid, clientItem.Definition, clientKey.Id);

        return true;
    }
}