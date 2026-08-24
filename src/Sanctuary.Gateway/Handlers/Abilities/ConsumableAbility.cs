using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Handlers.Abilities;

public abstract class ConsumableAbility
{
    // Set once from AbilityPacketClientRequestStartAbilityHandler.ConfigureServices.
    internal static ILogger _logger = null!;
    internal static IResourceManager _resourceManager = null!;
    internal static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    internal static int _castFxTagCounter = 5000;
    private static readonly ConcurrentDictionary<ulong, ConcurrentDictionary<int, DateTimeOffset>> _itemCooldowns = new();

    internal const int ActionBarId = 2;
    internal const int IdleAnimationId = 1;

    public abstract bool IsInCollection(ClientItemDefinition itemDefinition);

    public abstract bool HandleAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition);

    internal static bool IsOnCooldown(ulong playerGuid, int itemDefinitionId)
    {
        return _itemCooldowns.TryGetValue(playerGuid, out var cooldowns) &&
               cooldowns.TryGetValue(itemDefinitionId, out var expiry) &&
               DateTimeOffset.UtcNow < expiry;
    }

    internal static void StartCooldown(ulong playerGuid, int itemDefinitionId, int cooldownMs)
    {
        var cooldowns = _itemCooldowns.GetOrAdd(playerGuid, _ => new ConcurrentDictionary<int, DateTimeOffset>());

        cooldowns[itemDefinitionId] = DateTimeOffset.UtcNow.AddMilliseconds(cooldownMs);
    }

    internal static int IconTintId(ClientItem clientItem, int defaultTintId) =>
        clientItem.Tint == 0 ? defaultTintId : clientItem.Tint;

    internal static bool ConsumeItem(GatewayConnection connection, ClientItem clientItem, ClientItemDefinition clientItemDefinition, int actionBarSlot)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var dbItem = dbContext.Items.SingleOrDefault(i => i.CharacterId == characterId && i.Id == clientItem.Id);

        if (dbItem is null)
            return SendFailure(connection);

        dbItem.Count--;

        var shouldDeleteItem = dbItem.Count <= 0;

        if (shouldDeleteItem)
            dbContext.Items.Remove(dbItem);

        if (dbContext.SaveChanges() <= 0)
            return SendFailure(connection);

        if (shouldDeleteItem)
        {
            connection.Player.Items.Remove(clientItem);
            connection.SendTunneled(new ClientUpdatePacketItemDelete { ItemGuid = clientItem.Id });

            connection.Player.CancelScheduledSlotPacket(ActionBarId, actionBarSlot);

            var slotPacket = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = ActionBarId, Slot = actionBarSlot } };
            slotPacket.Slot.IsEmpty = true;

            if (connection.Player.ActionBarItemGuids.TryGetValue(ActionBarId, out var trackedItems))
                trackedItems.Remove(actionBarSlot);

            connection.SendTunneled(slotPacket);
        }
        else
        {
            clientItem.Count--;

            connection.SendTunneled(new ClientUpdatePacketItemUpdate
            {
                ItemGuid = clientItem.Id,
                Count = clientItem.Count,
                ConsumedCount = clientItem.ConsumedCount,
                AbilityCount = clientItem.AbilityCount,
                RentalExpirationTime = 0
            });

            var slotPacket = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = ActionBarId, Slot = actionBarSlot } };
            slotPacket.Slot.IsEmpty = false;
            slotPacket.Slot.IconId = clientItemDefinition.Icon.Id;
            slotPacket.Slot.IconTintId = IconTintId(clientItem, clientItemDefinition.Icon.TintId);
            slotPacket.Slot.NameId = clientItemDefinition.NameId;
            slotPacket.Slot.Unknown5 = 1;
            slotPacket.Slot.Unknown6 = 4;
            slotPacket.Slot.Unknown7 = 15;
            slotPacket.Slot.Enabled = true;
            slotPacket.Slot.Unknown10 = 1000;
            slotPacket.Slot.TotalRefreshTime = 1000;
            slotPacket.Slot.Quantity = clientItem.Count;
            slotPacket.Slot.ForceDismount = true;
            slotPacket.Slot.Unknown15 = 1000;

            connection.SendTunneled(slotPacket);
        }

        return true;
    }

    internal static void PlayEffect(GatewayConnection connection, int effectId, int delayMs = 0)
    {
        if (effectId == 0)
            return;

        var effectPacket = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = connection.Player.Guid,
            CompositeEffectId = effectId,
            Clear = true
        };

        if (delayMs > 0)
            connection.Player.SendTunneledToVisibleDelayed(effectPacket, delayMs, true);
        else
            connection.Player.SendTunneledToVisible(effectPacket, true);
    }

    internal static void DespawnNpc(Npc npc, int effectId)
    {
        var removePacket = new PlayerUpdatePacketRemovePlayerGracefully
        {
            Guid = npc.Guid,
            Animate = false,
            Delay = 0,
            EffectDelay = 0,
            CompositeEffectId = effectId,
            Duration = 500
        };

        foreach (var player in npc.Zone.Players)
            player.SendTunneled(removePacket);

        npc.Dispose();
    }

    protected Npc? SpawnNpc(GatewayConnection connection, Vector4 position, Action<Npc> configure)
    {
        if (connection.Player.Zone is not StartingZone startingZone)
            return null;

        if (!startingZone.TryCreateNpc(out var npc))
            return null;

        configure(npc);

        // Visible must be set before UpdatePosition so the zone tile system sends AddNpc to players in range.
        npc.Visible = true;
        npc.UpdatePosition(position, connection.Player.Rotation);

        return npc;
    }

    protected virtual List<Player> BroadcastSpawn(GatewayConnection connection, Npc npc, Vector4 position, int poofEffectId)
    {
        var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = npc.Guid,
            CompositeEffectId = poofEffectId,
            Position = position,
            Clear = false
        };

        connection.Player.SendTunneled(poofEffect);
        connection.Player.OnAddVisibleNpcs([npc]);

        var recipients = new List<Player> { connection.Player };

        foreach (var player in connection.Player.VisiblePlayers.Values)
        {
            player.SendTunneled(poofEffect);
            player.OnAddVisibleNpcs([npc]);
            recipients.Add(player);
        }

        return recipients;
    }

    internal static bool SendFailure(GatewayConnection connection)
    {
        connection.SendTunneled(new AbilityPacketFailed { StringId = 3079 });

        return true;
    }

    internal static void FinishActivation(GatewayConnection connection, ClientItem clientItem, ClientItemDefinition itemDefinition, int slot, int cooldownMs, int iconTintId = 0)
    {
        var count = clientItem.Count;
        var hasItemLeft = !itemDefinition.SingleUse || count > 1;

        if (itemDefinition.SingleUse)
            ConsumeItem(connection, clientItem, itemDefinition, slot);

        if (hasItemLeft)
            connection.Player.StartActionBarCooldown(ActionBarId, slot, itemDefinition.Icon.Id, itemDefinition.NameId,
                itemDefinition.SingleUse ? count - 1 : count, cooldownMs, iconTintId);
    }
}
