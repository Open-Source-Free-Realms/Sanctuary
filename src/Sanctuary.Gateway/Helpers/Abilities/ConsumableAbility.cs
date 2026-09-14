using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

public sealed record AbilityServices(
    ILogger Logger,
    IResourceManager ResourceManager,
    IDbContextFactory<DatabaseContext> DbContextFactory);

public abstract class ConsumableAbility(AbilityServices services)
{
    internal const int ActionBarId = 2;
    protected const int IdleAnimationId = 1;

    protected readonly ILogger _logger = services.Logger;
    protected readonly IResourceManager _resourceManager = services.ResourceManager;

    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory = services.DbContextFactory;

    public abstract bool Matches(ClientItemDefinition itemDefinition);

    public abstract bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition);

    protected static int NextEffectTagId() => EffectTagIdGenerator.Next();

    // Color-variant items (the 5 Silly String Can colors) share one Icon.Id and differ only by TintId.
    protected static int IconTintId(ClientItem clientItem, int defaultTintId) =>
        clientItem.Tint == 0 ? defaultTintId : clientItem.Tint;

    protected bool ConsumeItem(Player player, ClientItem clientItem, ClientItemDefinition clientItemDefinition, int actionBarSlot)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var characterId = GuidHelper.GetPlayerId(player.Guid);
        var dbItem = dbContext.Items.SingleOrDefault(i => i.CharacterId == characterId && i.Id == clientItem.Id);

        if (dbItem is null)
            return SendFailure(player);

        dbItem.Count--;

        var shouldDeleteItem = dbItem.Count <= 0;

        if (shouldDeleteItem)
            dbContext.Items.Remove(dbItem);

        if (dbContext.SaveChanges() <= 0)
            return SendFailure(player);

        if (shouldDeleteItem)
        {
            player.Items.Remove(clientItem);
            player.SendTunneled(new ClientUpdatePacketItemDelete { ItemGuid = clientItem.Id });

            // A pending cooldown re-enable would fire after this and un-delete the slot.
            player.CancelScheduledSlotPacket(ActionBarId, actionBarSlot);

            var slotPacket = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = ActionBarId, Slot = actionBarSlot } };
            slotPacket.Slot.IsEmpty = true;

            if (player.ActionBarItemGuids.TryGetValue(ActionBarId, out var trackedItems))
                trackedItems.Remove(actionBarSlot);

            player.SendTunneled(slotPacket);
        }
        else
        {
            clientItem.Count--;

            player.SendTunneled(new ClientUpdatePacketItemUpdate
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

            player.SendTunneled(slotPacket);
        }

        return true;
    }

    protected static void PlayEffect(Player player, int effectId, int delayMs = 0)
    {
        if (effectId == 0)
            return;

        var effectPacket = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = player.Guid,
            CompositeEffectId = effectId,
            Clear = true
        };

        if (delayMs > 0)
            player.SendTunneledToVisibleDelayed(effectPacket, delayMs, true);
        else
            player.SendTunneledToVisible(effectPacket, true);
    }

    protected static void DespawnNpc(Npc npc, int effectId)
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

    protected static Npc? SpawnNpc(Player player, Vector4 position, Action<Npc> configure)
    {
        if (player.Zone is not StartingZone startingZone)
            return null;

        if (!startingZone.TryCreateNpc(out var npc))
            return null;

        configure(npc);

        // Visible must be set before UpdatePosition so the zone tile system sends AddNpc to players in range.
        npc.Visible = true;
        npc.UpdatePosition(position, player.Rotation);

        return npc;
    }

    // SpawnNpc already sent AddNpc to everyone in tile range, so this only plays the poof and
    // covers the spawner if they ended up outside the NPC's tiles. Returns who got the spawn.
    protected static List<Player> BroadcastSpawn(Player player, Npc npc, Vector4 position, int poofEffectId)
    {
        var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = npc.Guid,
            CompositeEffectId = poofEffectId,
            Position = position,
            Clear = false
        };

        var recipients = npc.VisiblePlayers.Values.ToList();

        if (!npc.VisiblePlayers.ContainsKey(player.Guid))
        {
            player.SendTunneled(npc.GetAddNpcPacket());
            recipients.Insert(0, player);
        }

        foreach (var recipient in recipients)
            recipient.SendTunneled(poofEffect);

        return recipients;
    }

    // internal so the handler can fail its own request validation the same way.
    internal static bool SendFailure(Player player)
    {
        player.SendTunneled(new AbilityPacketFailed { StringId = 3079 });

        return true;
    }

    protected void FinishActivation(Player player, ClientItem clientItem, ClientItemDefinition itemDefinition, int slot, int cooldownMs, int iconTintId = 0)
    {
        var count = clientItem.Count;
        var hasItemLeft = !itemDefinition.SingleUse || count > 1;

        if (itemDefinition.SingleUse)
            ConsumeItem(player, clientItem, itemDefinition, slot);

        if (hasItemLeft)
            player.StartActionBarCooldown(ActionBarId, slot, itemDefinition.Icon.Id, itemDefinition.NameId,
                itemDefinition.SingleUse ? count - 1 : count, cooldownMs, iconTintId);
    }
}
