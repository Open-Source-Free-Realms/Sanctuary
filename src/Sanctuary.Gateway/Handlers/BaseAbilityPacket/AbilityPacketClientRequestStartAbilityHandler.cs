using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AbilityPacketClientRequestStartAbilityHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    private static readonly ConcurrentDictionary<ulong, ConcurrentDictionary<int, DateTimeOffset>> _itemCooldowns = new();

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(AbilityPacketClientRequestStartAbilityHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!AbilityPacketClientRequestStartAbility.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(AbilityPacketClientRequestStartAbility));
            return false;
        }

        _logger.LogInformation("AbilityPacket: Id={Id} Slot={Slot}", packet.Data.Id, packet.Data.Slot);

        if (packet.Data.Id == 2)
            return HandleItemAbility(connection, packet);

        connection.SendTunneled(new AbilityPacketFailed { StringId = 3079 });
        return true;
    }

    private static bool HandleItemAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet)
    {
        connection.Player.ActionBars.TryGetValue(2, out var actionBar);

        if (actionBar is null || !actionBar.Slots.TryGetValue(packet.Data.Slot, out var foundSlot) || foundSlot.IsEmpty)
        {
            SendFailure(connection, 3079);
            return true;
        }

        if (!connection.Player.ActionBarItemGuids.TryGetValue(2, out var actionBarItems) ||
            !actionBarItems.TryGetValue(packet.Data.Slot, out var itemGuid))
        {
            SendFailure(connection, 3079);
            return true;
        }

        var clientItem = connection.Player.Items.FirstOrDefault(x => x.Id == itemGuid);
        if (clientItem is null)
        {
            SendFailure(connection, 3079);
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            SendFailure(connection, 3079);
            return true;
        }

        if (clientItemDefinition.ActivatableAbilityId == 0)
        {
            SendFailure(connection, 3079);
            return true;
        }

        bool isBoombox = _resourceManager.Consumables.Boomboxes.ContainsKey(clientItemDefinition.Id);
        bool isCake = _resourceManager.Consumables.Cakes.TryGetValue(clientItemDefinition.Id, out var cakeDef);

        if (isCake || isBoombox)
        {
            var playerCooldowns = _itemCooldowns.GetOrAdd(connection.Player.Guid, _ => new ConcurrentDictionary<int, DateTimeOffset>());

            if (playerCooldowns.TryGetValue(clientItemDefinition.Id, out var expiry) && DateTimeOffset.UtcNow < expiry)
            {
                SendFailure(connection, 3079);
                return true;
            }

            if (isCake)
                SpawnCakeNpc(connection, cakeDef!);
            else
                SpawnBoomboxNpc(connection, clientItemDefinition);

            int cooldownMs = isCake ? cakeDef!.CooldownMs : 60_000;
            playerCooldowns[clientItemDefinition.Id] = DateTimeOffset.UtcNow.AddMilliseconds(cooldownMs);
            connection.Player.StartActionBarCooldown(2, packet.Data.Slot, clientItemDefinition.Icon.Id, clientItemDefinition.NameId, clientItem.Count, cooldownMs);

            return true;
        }

        _logger.LogInformation("HandleItemAbility: itemDef={ItemDefId} abilityId={AbilityId}", clientItemDefinition.Id, clientItemDefinition.ActivatableAbilityId);

        if (_resourceManager.Consumables.Transformations.TryGetValue(clientItemDefinition.ActivatableAbilityId, out var transform))
        {
            _logger.LogInformation("Transform match: modelId={ModelId} durationMs={Duration}", transform.ModelId, transform.DurationMs);

            var playerCooldowns = _itemCooldowns.GetOrAdd(connection.Player.Guid, _ => new ConcurrentDictionary<int, DateTimeOffset>());

            if (playerCooldowns.TryGetValue(clientItemDefinition.Id, out var expiry) && DateTimeOffset.UtcNow < expiry)
            {
                SendFailure(connection, 3079);
                return true;
            }

            if (connection.Player.TemporaryAppearance != 0)
            {
                SendFailure(connection, 3079);
                return true;
            }

            ApplyTransform(connection, transform.ModelId, transform.DurationMs, transform.CompositeEffectId);

            playerCooldowns[clientItemDefinition.Id] = DateTimeOffset.UtcNow.AddMilliseconds(transform.CooldownMs);

            var capturedSlot = packet.Data.Slot;
            var capturedItemDef = clientItemDefinition;
            var capturedCount = clientItem.Count;
            var cooldownMs = transform.CooldownMs;

            bool willHaveItemLeft = capturedCount > 1;

            if (clientItemDefinition.SingleUse)
                ConsumeItem(connection, clientItem, clientItemDefinition, capturedSlot);

            if (willHaveItemLeft)
                connection.Player.StartActionBarCooldown(2, capturedSlot, capturedItemDef.Icon.Id, capturedItemDef.NameId, capturedCount - 1, cooldownMs);

            return true;
        }

        TriggerAbilityEffect(connection, clientItemDefinition);

        if (clientItemDefinition.SingleUse)
            return ConsumeItem(connection, clientItem, clientItemDefinition, packet.Data.Slot);

        return true;
    }

    private static bool ConsumeItem(GatewayConnection connection, Packet.Common.ClientItem clientItem,
        Packet.Common.ClientItemDefinition clientItemDefinition, int actionBarSlot)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var dbItem = dbContext.Items.SingleOrDefault(i => i.CharacterId == characterId && i.Id == clientItem.Id);

        if (dbItem is null)
        {
            SendFailure(connection, 3079);
            return true;
        }

        dbItem.Count--;

        var shouldDeleteItem = dbItem.Count <= 0;

        if (shouldDeleteItem)
            dbContext.Items.Remove(dbItem);

        if (dbContext.SaveChanges() <= 0)
        {
            SendFailure(connection, 3079);
            return true;
        }

        if (shouldDeleteItem)
        {
            connection.Player.Items.Remove(clientItem);
            connection.SendTunneled(new ClientUpdatePacketItemDelete { ItemGuid = clientItem.Id });

            var slotPacket = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = actionBarSlot } };
            slotPacket.Slot.IsEmpty = true;

            if (connection.Player.ActionBarItemGuids.TryGetValue(2, out var trackedItems))
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

            var slotPacket = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = actionBarSlot } };
            slotPacket.Slot.IsEmpty = false;
            slotPacket.Slot.IconId = clientItemDefinition.Icon.Id;
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

    private static void TriggerAbilityEffect(GatewayConnection connection, Packet.Common.ClientItemDefinition clientItemDefinition)
    {
        if (clientItemDefinition.ActivatableAbilityId == 0)
            return;

        int abilityId = clientItemDefinition.ActivatableAbilityId;

        _resourceManager.Consumables.FoodEffects.TryGetValue(abilityId, out var foodEffect);

        int effectId = foodEffect?.CompositeEffectId ?? clientItemDefinition.CompositeEffectId;
        int quickChatId = foodEffect?.QuickChatId ?? 0;
        int effectDelayMs = foodEffect?.EffectDelayMs ?? 0;

        if (quickChatId != 0)
        {
            var animPacket = new QuickChatSendChatToChannelPacket
            {
                Id = quickChatId,
                Guid = connection.Player.Guid,
                Name = connection.Player.Name ?? new Packet.Common.NameData(),
                Channel = Packet.Common.Chat.ChatChannel.WorldArea,
                AreaNameId = 0,
                GuildGuid = 0
            };
            connection.Player.SendTunneledToVisible(animPacket, true);
        }
        else
        {
            connection.Player.SendTunneledToVisible(new AbilityPacketExecuteClientLua { Script = string.Empty, Param1 = 0, Param2 = 0, Param3 = 0 }, true);
        }

        if (effectId != 0)
        {
            var effectPacket = new PlayerUpdatePacketPlayCompositeEffect { Guid = connection.Player.Guid, CompositeEffectId = effectId, Clear = true };

            if (effectDelayMs > 0)
                Task.Delay(effectDelayMs).ContinueWith(_ => connection.Player.SendTunneledToVisible(effectPacket, true));
            else
                connection.Player.SendTunneledToVisible(effectPacket, true);
        }
    }

    private static void SpawnCakeNpc(GatewayConnection connection, CakeItemDefinition cakeDef)
    {
        try
        {
            var zone = connection.Player.Zone;
            if (zone is not Game.Zones.StartingZone startingZone)
                return;

            if (!startingZone.TryCreateNpc(out var cakeNpc))
                return;

            cakeNpc.NameId = cakeDef.NameId;
            cakeNpc.ModelId = cakeDef.ModelId;
            cakeNpc.TextureAlias = "";
            cakeNpc.TintAlias = "";
            cakeNpc.Scale = 1.0f;
            cakeNpc.Animation = cakeDef.Animation;
            cakeNpc.HideNamePlate = false;
            cakeNpc.IsInteractable = true;
            cakeNpc.CursorId = (byte)cakeDef.CursorId;

            var forwardDirection = Vector3.Transform(new Vector3(0, 0, 1), connection.Player.Rotation);
            var spawnPosition = new Vector4(
                connection.Player.Position.X + forwardDirection.X * 1.5f,
                connection.Player.Position.Y + forwardDirection.Y * 1.5f,
                connection.Player.Position.Z + forwardDirection.Z * 1.5f,
                connection.Player.Position.W
            );

            cakeNpc.Visible = true;
            cakeNpc.UpdatePosition(spawnPosition, connection.Player.Rotation);

            if (cakeDef.Type == CakeItemType.BossCake)
            {
                cakeNpc.InteractAction = (interactingPlayer) =>
                {
                    int abilityId = cakeDef.TransformAbilityIds[Random.Shared.Next(cakeDef.TransformAbilityIds.Length)];
                    if (_resourceManager.Consumables.Transformations.TryGetValue(abilityId, out var transform))
                        ApplyTransform(interactingPlayer, transform.ModelId, transform.DurationMs, transform.CompositeEffectId);
                };
            }
            else
            {
                var scareActive = false;
                cakeNpc.InteractAction = (interactingPlayer) =>
                {
                    if (scareActive) return;
                    scareActive = true;

                    var cakePosition = cakeNpc.Position;

                    void Broadcast(ISerializablePacket pkt)
                    {
                        interactingPlayer.SendTunneled(pkt);
                        foreach (var p in interactingPlayer.VisiblePlayers.Values)
                            p.SendTunneled(pkt);
                    }

                    void SendEffect(int effectId) => Broadcast(new PlayerUpdatePacketPlayCompositeEffect
                    {
                        Guid = cakeNpc.Guid,
                        CompositeEffectId = effectId,
                        Position = cakePosition,
                        Clear = true
                    });

                    var group = cakeDef.ScareGroups[Random.Shared.Next(cakeDef.ScareGroups.Length)];
                    foreach (var effectId in group)
                        SendEffect(effectId);

                    Task.Delay(cakeDef.ScareCooldownMs).ContinueWith(_ => { scareActive = false; });
                };
            }

            var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = cakeNpc.Guid,
                CompositeEffectId = cakeDef.SpawnPoofEffectId,
                Position = spawnPosition,
                Clear = false
            };

            connection.Player.SendTunneled(poofEffect);
            connection.Player.OnAddVisibleNpcs([cakeNpc]);
            foreach (var player in connection.Player.VisiblePlayers.Values)
            {
                player.SendTunneled(poofEffect);
                player.OnAddVisibleNpcs([cakeNpc]);
            }

            var capturedNpc = cakeNpc;

            Task.Delay(cakeDef.LifetimeMs).ContinueWith(_ =>
            {
                try
                {
                    var removePacket = new PlayerUpdatePacketRemovePlayerGracefully
                    {
                        Guid = capturedNpc.Guid,
                        Animate = false,
                        Delay = 0,
                        EffectDelay = 0,
                        CompositeEffectId = cakeDef.SpawnPoofEffectId,
                        Duration = 500
                    };

                    foreach (var player in startingZone.Players ?? [])
                        player.SendTunneled(removePacket);

                    capturedNpc.Dispose();
                }
                catch (Exception ex) { _logger.LogError(ex, "SpawnCakeNpc: error during NPC cleanup"); }
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "SpawnCakeNpc: error during NPC spawn"); }
    }

    private static void SpawnBoomboxNpc(GatewayConnection connection, Packet.Common.ClientItemDefinition itemDef)
    {
        try
        {
            var zone = connection.Player.Zone;
            if (zone is not Game.Zones.StartingZone startingZone)
                return;

            if (!startingZone.TryCreateNpc(out var boomboxNpc))
                return;

            _resourceManager.Consumables.Boomboxes.TryGetValue(itemDef.Id, out var boomboxDef);
            int modelId = boomboxDef?.ModelId ?? 1062;
            int effectId = boomboxDef?.EffectId ?? 0;
            int[] danceSequence = boomboxDef?.DanceSequence ?? [3501, 3502, 3503, 3504, 3505];

            boomboxNpc.NameId = 0;
            boomboxNpc.ModelId = modelId;
            boomboxNpc.Name = "Boombox";
            boomboxNpc.TextureAlias = itemDef.TextureAlias ?? "";
            boomboxNpc.TintAlias = itemDef.TintAlias ?? "";
            boomboxNpc.Scale = 1.0f;
            boomboxNpc.Animation = 2100; // Bouncing animation
            boomboxNpc.CompositeEffectId = effectId; // Owned by the entity — client stops it when RemovePlayer is received
            boomboxNpc.HideNamePlate = true;
            boomboxNpc.IsInteractable = false;

            var leftDirection = Vector3.Transform(new Vector3(-1, 0, 0), connection.Player.Rotation);
            var spawnPosition = new Vector4(
                connection.Player.Position.X + leftDirection.X * 2.0f,
                connection.Player.Position.Y + leftDirection.Y * 2.0f,
                connection.Player.Position.Z + leftDirection.Z * 2.0f,
                connection.Player.Position.W
            );

            // Visible must be set before UpdatePosition so UpdateZoneTile() fires
            // and the zone tile system sends AddNpc to all players in range.
            boomboxNpc.Visible = true;
            boomboxNpc.UpdatePosition(spawnPosition, connection.Player.Rotation);

            var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = boomboxNpc.Guid,
                CompositeEffectId = 21, // PFX_smoke_black_explosion
                Position = spawnPosition,
                Clear = false
            };

            // Zone tile system sent AddNpc to boomboxNpc.VisiblePlayers via OnAddVisibleNpcs.
            // Send poof after AddNpc so the entity exists on the client when the effect plays.
            var pktRecipients = boomboxNpc.VisiblePlayers.Values.ToList();
            if (!boomboxNpc.VisiblePlayers.ContainsKey(connection.Player.Guid))
            {
                // Spawner is outside zone tile range — send packets manually.
                connection.Player.SendTunneled(boomboxNpc.GetAddNpcPacket());
                pktRecipients.Insert(0, connection.Player);
            }

            foreach (var player in pktRecipients)
                player.SendTunneled(poofEffect);

            var capturedNpc = boomboxNpc;

            const float BoomboxRangeInMeters = 15.0f;
            const int DanceInterval = 3000;
            int iterations = 60_000 / DanceInterval;

            Task.Run(async () =>
            {
                try
                {
                    int sequenceIndex = 0;
                    for (int i = 0; i < iterations; i++)
                    {
                        await Task.Delay(DanceInterval);

                        int quickChatId = danceSequence[sequenceIndex];
                        sequenceIndex = (sequenceIndex + 1) % danceSequence.Length;

                        var playersInRange = (startingZone.Players ?? []).Where(p =>
                            Vector3.Distance(new Vector3(p.Position.X, p.Position.Y, p.Position.Z),
                                             new Vector3(spawnPosition.X, spawnPosition.Y, spawnPosition.Z))
                            <= BoomboxRangeInMeters).ToList();

                        foreach (var player in playersInRange)
                        {
                            try
                            {
                                var dancePacket = new QuickChatSendChatToChannelPacket
                                {
                                    Id = quickChatId,
                                    Guid = player.Guid,
                                    Name = player.Name ?? new Packet.Common.NameData(),
                                    Channel = Packet.Common.Chat.ChatChannel.WorldArea,
                                    AreaNameId = 0,
                                    GuildGuid = 0
                                };
                                player.SendTunneled(dancePacket);
                                foreach (var visiblePlayer in player.VisiblePlayers.Values)
                                    visiblePlayer.SendTunneled(dancePacket);
                            }
                            catch (Exception ex) { _logger.LogError(ex, "SpawnBoomboxNpc: error sending dance packet to player {Guid}", player.Guid); }
                        }
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "SpawnBoomboxNpc: unhandled error in dance loop"); }
            });

            Task.Delay(60_000).ContinueWith(_ =>
            {
                try
                {
                    // CompositeEffectId=21 plays the despawn poof on the way out.
                    var removePacket = new PlayerUpdatePacketRemovePlayerGracefully
                    {
                        Guid = capturedNpc.Guid,
                        Animate = false,
                        Delay = 0,
                        EffectDelay = 0,
                        CompositeEffectId = 21, // PFX_smoke_black_explosion despawn poof
                        Duration = 500
                    };

                    foreach (var player in startingZone.Players ?? [])
                        player.SendTunneled(removePacket);

                    capturedNpc.Dispose();
                }
                catch (Exception ex) { _logger.LogError(ex, "SpawnBoomboxNpc: error during NPC cleanup"); }
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "SpawnBoomboxNpc: error during NPC spawn"); }
    }

    private static void SendFailure(GatewayConnection connection, int stringId)
        => connection.SendTunneled(new AbilityPacketFailed { StringId = stringId });

    internal static void ApplyTransform(GatewayConnection connection, int temporaryAppearance, int durationMs, int effectId = 0)
        => connection.Player.ApplyTemporaryAppearance(temporaryAppearance, durationMs, effectId);

    internal static void ApplyTransform(Game.Entities.Player player, int temporaryAppearance, int durationMs, int effectId = 0)
        => player.ApplyTemporaryAppearance(temporaryAppearance, durationMs, effectId);

    internal static void RemoveTransform(GatewayConnection connection)
        => connection.Player.RemoveTemporaryAppearance();
}
