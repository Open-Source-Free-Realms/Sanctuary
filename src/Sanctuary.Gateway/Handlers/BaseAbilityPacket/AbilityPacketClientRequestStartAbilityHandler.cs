using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    private static readonly ConcurrentDictionary<ulong, ConcurrentDictionary<int, DateTimeOffset>> _boomboxCooldowns = new();

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
        {
            return HandleItemAbility(connection, packet);
        }

        var abilityPacketFailed = new AbilityPacketFailed
        {
            StringId = 3079
        };

        connection.SendTunneled(abilityPacketFailed);

        return true;
    }

    private static bool HandleItemAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet)
    {
        connection.Player.ActionBars.TryGetValue(2, out var actionBar);

        Packet.Common.ActionBarSlot? slot = null;
        if (actionBar != null && actionBar.Slots.TryGetValue(packet.Data.Slot, out var foundSlot) && !foundSlot.IsEmpty)
        {
            slot = foundSlot;
        }
        else
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
        bool isScaredyCake = isCake && cakeDef!.Type == CakeItemType.ScaredyCake;
        bool isBossCake = isCake && cakeDef!.Type == CakeItemType.BossCake;

        if (isCake || isBoombox)
        {
            var playerCooldowns = _boomboxCooldowns.GetOrAdd(connection.Player.Guid, _ => new ConcurrentDictionary<int, DateTimeOffset>());

            if (playerCooldowns.TryGetValue(clientItemDefinition.Id, out var expiry) && DateTimeOffset.UtcNow < expiry)
            {
                SendFailure(connection, 3079);
                return true;
            }

            if (isBossCake)
                SpawnBossCakeNpc(connection);
            else if (isScaredyCake)
                SpawnCakeNpc(connection);
            else
                SpawnBoomboxNpc(connection, clientItemDefinition);

            int cooldownMs = isCake ? cakeDef!.CooldownMs : 60_000;
            playerCooldowns[clientItemDefinition.Id] = DateTimeOffset.UtcNow.AddMilliseconds(cooldownMs);

            var capturedSlot = packet.Data.Slot;
            var capturedItemDef = clientItemDefinition;
            var capturedCount = clientItem.Count;
            var capturedPlayerGuid = connection.Player.Guid;

            var cooldownSlot = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = capturedSlot } };
            cooldownSlot.Slot.IsEmpty = false;
            cooldownSlot.Slot.IconId = capturedItemDef.Icon.Id;
            cooldownSlot.Slot.NameId = capturedItemDef.NameId;
            cooldownSlot.Slot.Unknown5 = 1;
            cooldownSlot.Slot.Unknown6 = 4;
            cooldownSlot.Slot.Unknown7 = 15;
            cooldownSlot.Slot.Enabled = false;
            cooldownSlot.Slot.Unknown10 = 0;
            cooldownSlot.Slot.TotalRefreshTime = cooldownMs;
            cooldownSlot.Slot.Unknown12 = 0;
            cooldownSlot.Slot.Quantity = capturedCount;
            cooldownSlot.Slot.ForceDismount = true;
            cooldownSlot.Slot.Unknown15 = 0;
            connection.SendTunneled(cooldownSlot);

            var startTime = DateTimeOffset.UtcNow;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        await Task.Delay(2000);
                        int elapsed = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                        if (elapsed >= cooldownMs) break;

                        var tickSlot = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = capturedSlot } };
                        tickSlot.Slot.IsEmpty = false;
                        tickSlot.Slot.IconId = capturedItemDef.Icon.Id;
                        tickSlot.Slot.NameId = capturedItemDef.NameId;
                        tickSlot.Slot.Unknown5 = 1;
                        tickSlot.Slot.Unknown6 = 4;
                        tickSlot.Slot.Unknown7 = 15;
                        tickSlot.Slot.Enabled = false;
                        tickSlot.Slot.Unknown10 = elapsed;
                        tickSlot.Slot.TotalRefreshTime = cooldownMs;
                        tickSlot.Slot.Unknown12 = elapsed;
                        tickSlot.Slot.Quantity = capturedCount;
                        tickSlot.Slot.ForceDismount = true;
                        tickSlot.Slot.Unknown15 = elapsed;
                        connection.SendTunneled(tickSlot);
                    }
                }
                catch { }
            });

            Task.Delay(cooldownMs).ContinueWith(_ =>
            {
                try
                {
                    if (_boomboxCooldowns.TryGetValue(capturedPlayerGuid, out var cd))
                        cd.TryRemove(capturedItemDef.Id, out DateTimeOffset _);

                    var readySlot = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = capturedSlot } };
                    readySlot.Slot.IsEmpty = false;
                    readySlot.Slot.IconId = capturedItemDef.Icon.Id;
                    readySlot.Slot.NameId = capturedItemDef.NameId;
                    readySlot.Slot.Unknown5 = 1;
                    readySlot.Slot.Unknown6 = 4;
                    readySlot.Slot.Unknown7 = 15;
                    readySlot.Slot.Enabled = true;
                    readySlot.Slot.Unknown10 = 1000;
                    readySlot.Slot.TotalRefreshTime = 1000;
                    readySlot.Slot.Quantity = capturedCount;
                    readySlot.Slot.ForceDismount = true;
                    readySlot.Slot.Unknown15 = 1000;
                    connection.SendTunneled(readySlot);
                }
                catch { }
            });

            return true;
        }

        _logger.LogInformation("HandleItemAbility: itemDef={ItemDefId} abilityId={AbilityId}", clientItemDefinition.Id, clientItemDefinition.ActivatableAbilityId);

        if (_resourceManager.Consumables.Transformations.TryGetValue(clientItemDefinition.ActivatableAbilityId, out var transform))
        {
            _logger.LogInformation("Transform match: modelId={ModelId} durationMs={Duration}", transform.ModelId, transform.DurationMs);
            var playerCooldowns = _boomboxCooldowns.GetOrAdd(connection.Player.Guid, _ => new ConcurrentDictionary<int, DateTimeOffset>());

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

            ApplyTransform(connection, transform.ModelId, transform.DurationMs);

            playerCooldowns[clientItemDefinition.Id] = DateTimeOffset.UtcNow.AddMilliseconds(transform.CooldownMs);

            var capturedSlot = packet.Data.Slot;
            var capturedItemDef = clientItemDefinition;
            var capturedCount = clientItem.Count;
            var capturedPlayerGuid = connection.Player.Guid;
            var cooldownMs = transform.CooldownMs;

            bool willHaveItemLeft = capturedCount > 1;

            if (clientItemDefinition.SingleUse)
                ConsumeItem(connection, clientItem, clientItemDefinition, capturedSlot);

            if (willHaveItemLeft)
            {
                var startTime = DateTimeOffset.UtcNow;

                var cooldownSlot = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = capturedSlot } };
                cooldownSlot.Slot.IsEmpty = false;
                cooldownSlot.Slot.IconId = capturedItemDef.Icon.Id;
                cooldownSlot.Slot.NameId = capturedItemDef.NameId;
                cooldownSlot.Slot.Unknown5 = 1;
                cooldownSlot.Slot.Unknown6 = 4;
                cooldownSlot.Slot.Unknown7 = 15;
                cooldownSlot.Slot.Enabled = false;
                cooldownSlot.Slot.Unknown10 = 0;
                cooldownSlot.Slot.TotalRefreshTime = cooldownMs;
                cooldownSlot.Slot.Unknown12 = 0;
                cooldownSlot.Slot.Quantity = capturedCount - 1;
                cooldownSlot.Slot.ForceDismount = true;
                cooldownSlot.Slot.Unknown15 = 0;
                connection.SendTunneled(cooldownSlot);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            await Task.Delay(2000);
                            int elapsed = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                            if (elapsed >= cooldownMs) break;

                            var tickSlot = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = capturedSlot } };
                            tickSlot.Slot.IsEmpty = false;
                            tickSlot.Slot.IconId = capturedItemDef.Icon.Id;
                            tickSlot.Slot.NameId = capturedItemDef.NameId;
                            tickSlot.Slot.Unknown5 = 1;
                            tickSlot.Slot.Unknown6 = 4;
                            tickSlot.Slot.Unknown7 = 15;
                            tickSlot.Slot.Enabled = false;
                            tickSlot.Slot.Unknown10 = elapsed;
                            tickSlot.Slot.TotalRefreshTime = cooldownMs;
                            tickSlot.Slot.Unknown12 = elapsed;
                            tickSlot.Slot.Quantity = capturedCount - 1;
                            tickSlot.Slot.ForceDismount = true;
                            tickSlot.Slot.Unknown15 = elapsed;
                            connection.SendTunneled(tickSlot);
                        }
                    }
                    catch { }
                });

                Task.Delay(cooldownMs).ContinueWith(_ =>
                {
                    try
                    {
                        if (_boomboxCooldowns.TryGetValue(capturedPlayerGuid, out var cd))
                            cd.TryRemove(capturedItemDef.Id, out DateTimeOffset _);

                        var readySlot = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = 2, Slot = capturedSlot } };
                        readySlot.Slot.IsEmpty = false;
                        readySlot.Slot.IconId = capturedItemDef.Icon.Id;
                        readySlot.Slot.NameId = capturedItemDef.NameId;
                        readySlot.Slot.Unknown5 = 1;
                        readySlot.Slot.Unknown6 = 4;
                        readySlot.Slot.Unknown7 = 15;
                        readySlot.Slot.Enabled = true;
                        readySlot.Slot.Unknown10 = 1000;
                        readySlot.Slot.TotalRefreshTime = 1000;
                        readySlot.Slot.Quantity = capturedCount - 1;
                        readySlot.Slot.ForceDismount = true;
                        readySlot.Slot.Unknown15 = 1000;
                        connection.SendTunneled(readySlot);
                    }
                    catch { }
                });
            }
            else
            {
                Task.Delay(cooldownMs).ContinueWith(_ =>
                {
                    if (_boomboxCooldowns.TryGetValue(capturedPlayerGuid, out var cd))
                        cd.TryRemove(capturedItemDef.Id, out DateTimeOffset _);
                });
            }

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
        {
            dbContext.Items.Remove(dbItem);
        }

        if (dbContext.SaveChanges() <= 0)
        {
            SendFailure(connection, 3079);
            return true;
        }

        if (shouldDeleteItem)
        {
            connection.Player.Items.Remove(clientItem);

            var clientUpdatePacketItemDelete = new ClientUpdatePacketItemDelete
            {
                ItemGuid = clientItem.Id
            };

            connection.SendTunneled(clientUpdatePacketItemDelete);

            var clientUpdatePacketUpdateActionBarSlot = new ClientUpdatePacketUpdateActionBarSlot
            {
                Data =
                {
                    Id = 2,
                    Slot = actionBarSlot
                }
            };

            clientUpdatePacketUpdateActionBarSlot.Slot.IsEmpty = true;

            if (connection.Player.ActionBarItemGuids.TryGetValue(2, out var trackedItems))
            {
                trackedItems.Remove(actionBarSlot);
            }

            connection.SendTunneled(clientUpdatePacketUpdateActionBarSlot);
        }
        else
        {
            clientItem.Count--;

            var clientUpdatePacketItemUpdate = new ClientUpdatePacketItemUpdate
            {
                ItemGuid = clientItem.Id,
                Count = clientItem.Count,
                ConsumedCount = clientItem.ConsumedCount,
                AbilityCount = clientItem.AbilityCount,
                RentalExpirationTime = 0
            };

            connection.SendTunneled(clientUpdatePacketItemUpdate);

            var clientUpdatePacketUpdateActionBarSlot = new ClientUpdatePacketUpdateActionBarSlot
            {
                Data =
                {
                    Id = 2,
                    Slot = actionBarSlot
                }
            };

            clientUpdatePacketUpdateActionBarSlot.Slot.IsEmpty = false;
            clientUpdatePacketUpdateActionBarSlot.Slot.IconId = clientItemDefinition.Icon.Id;
            clientUpdatePacketUpdateActionBarSlot.Slot.NameId = clientItemDefinition.NameId;
            clientUpdatePacketUpdateActionBarSlot.Slot.Unknown5 = 1;
            clientUpdatePacketUpdateActionBarSlot.Slot.Unknown6 = 4;
            clientUpdatePacketUpdateActionBarSlot.Slot.Unknown7 = 15;
            clientUpdatePacketUpdateActionBarSlot.Slot.Enabled = true;
            clientUpdatePacketUpdateActionBarSlot.Slot.Unknown10 = 1000;
            clientUpdatePacketUpdateActionBarSlot.Slot.TotalRefreshTime = 1000;
            clientUpdatePacketUpdateActionBarSlot.Slot.Quantity = clientItem.Count;
            clientUpdatePacketUpdateActionBarSlot.Slot.ForceDismount = true;
            clientUpdatePacketUpdateActionBarSlot.Slot.Unknown15 = 1000;

            connection.SendTunneled(clientUpdatePacketUpdateActionBarSlot);
        }

        return true;
    }

    private static void TriggerAbilityEffect(GatewayConnection connection, Packet.Common.ClientItemDefinition clientItemDefinition)
    {
        if (clientItemDefinition.ActivatableAbilityId == 0)
            return;

        int abilityId = clientItemDefinition.ActivatableAbilityId;

        if (abilityId == 660) // Can of Beans - fart effect
        {
            var fartAnimation = new QuickChatSendChatToChannelPacket
            {
                Id = 3340, // emo_fart
                Guid = connection.Player.Guid,
                Name = connection.Player.Name ?? new Packet.Common.NameData(),
                Channel = Packet.Common.Chat.ChatChannel.WorldArea,
                AreaNameId = 0,
                GuildGuid = 0
            };
            connection.SendTunneled(fartAnimation);
            connection.Player.SendToVisible(fartAnimation, false);

            System.Threading.Tasks.Task.Delay(300).ContinueWith(_ =>
            {
                var fartEffect = new PlayerUpdatePacketPlayCompositeEffect
                {
                    Guid = connection.Player.Guid,
                    CompositeEffectId = 5343,
                    Clear = true
                };
                connection.SendTunneled(fartEffect);
                connection.Player.SendToVisible(fartEffect, false);
            });
        }
        else if (abilityId == 1964) // Graveyard Flambe - shadow flames
        {
            var flameEffect = new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = connection.Player.Guid,
                CompositeEffectId = 5265,
                Clear = true
            };
            connection.SendTunneled(flameEffect);
            connection.Player.SendToVisible(flameEffect, false);

            _logger.LogInformation($"Player {connection.Player.Name.FirstName} activated shadow flames");
        }
        else
        {
            var abilityPacketExecuteClientLua = new AbilityPacketExecuteClientLua
            {
                Script = string.Empty,
                Param1 = 0,
                Param2 = 0,
                Param3 = 0
            };

            connection.Player.SendTunneledToVisible(abilityPacketExecuteClientLua, true);

            int effectId = 0;
            if (_resourceManager.Consumables.FoodEffects.TryGetValue(abilityId, out var foodEffect))
                effectId = foodEffect.CompositeEffectId;
            else if (clientItemDefinition.CompositeEffectId != 0)
                effectId = clientItemDefinition.CompositeEffectId;

            if (effectId != 0)
            {
                var playerUpdatePacketPlayCompositeEffect = new PlayerUpdatePacketPlayCompositeEffect
                {
                    Guid = connection.Player.Guid,
                    CompositeEffectId = effectId,
                    Clear = true, // attach to entity skeleton, not world position
                };

                connection.Player.SendTunneledToVisible(playerUpdatePacketPlayCompositeEffect, true);
            }
        }
    }

    private static void SpawnCakeNpc(GatewayConnection connection)
    {
        try
        {
            var zone = connection.Player.Zone;

            if (zone is not Game.Zones.StartingZone startingZone)
                return;

            if (!startingZone.TryCreateNpc(out var cakeNpc))
                return;

            cakeNpc.NameId = 16634; // "Scaredy Cake"
            cakeNpc.ModelId = 1724; // evnt_halloween_cake_01.adr
            cakeNpc.TextureAlias = "";
            cakeNpc.TintAlias = "";
            cakeNpc.Scale = 1.0f;
            cakeNpc.Animation = 1;
            cakeNpc.HideNamePlate = false;
            cakeNpc.IsInteractable = true;
            cakeNpc.CursorId = 5; // triggers NpcRelevance packet ? client shows "Press X"

            var forwardDirection = Vector3.Transform(new Vector3(0, 0, 1), connection.Player.Rotation);
            var spawnPosition = new Vector4(
                connection.Player.Position.X + forwardDirection.X * 1.5f,
                connection.Player.Position.Y + forwardDirection.Y * 1.5f,
                connection.Player.Position.Z + forwardDirection.Z * 1.5f,
                connection.Player.Position.W
            );

            cakeNpc.Visible = true;
            cakeNpc.UpdatePosition(spawnPosition, connection.Player.Rotation);

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

                switch (Random.Shared.Next(3))
                {
                    case 0: // Bats
                        SendEffect(5455);  // EFX_bats_exp_flyaway     (one-shot)
                        SendEffect(5165);  // SFX_OS_BatChirps         (one-shot)
                        SendEffect(15923); // PFX_halloween_icing_splat_cog       (one-shot)
                        break;
                    case 1: // Ghost
                        SendEffect(15909); // PFX_halloween_ghosts_up_loop
                        SendEffect(15960); // SFX_Halloween_GhostCrys
                        SendEffect(15961); // SFX_Halloween_GhostMoans
                        break;
                    case 2: // Raven
                        SendEffect(15744); // EFX_birds_pink_med_flyaway (one-shot, birds scatter)
                        SendEffect(5118);  // SFX_BirdCrowCaw           (one-shot)
                        SendEffect(15959); // SFX_Halloween_Crows        (one-shot)
                        break;
                }

                Task.Delay(2000).ContinueWith(_ => { scareActive = false; });
            };

            var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = cakeNpc.Guid,
                CompositeEffectId = 21, // PFX_smoke_black_explosion
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

            Task.Delay(60_000).ContinueWith(_ =>
            {
                try
                {
                    var removePacket = new PlayerUpdatePacketRemovePlayerGracefully
                    {
                        Guid = capturedNpc.Guid,
                        Animate = false,
                        Delay = 0,
                        EffectDelay = 0,
                        CompositeEffectId = 21,
                        Duration = 500
                    };

                    var players = startingZone.Players;
                    if (players is not null)
                    {
                        foreach (var player in players)
                            player.SendTunneled(removePacket);
                    }

                    capturedNpc.Dispose();
                }
                catch { }
            });
        }
        catch { }
    }

    private static void SpawnBossCakeNpc(GatewayConnection connection)
    {
        try
        {
            var zone = connection.Player.Zone;

            if (zone is not Game.Zones.StartingZone startingZone)
                return;

            if (!startingZone.TryCreateNpc(out var cakeNpc))
                return;

            cakeNpc.NameId = 16635; // Boss Cake
            cakeNpc.ModelId = 1724; // evnt_halloween_cake_01.adr
            cakeNpc.TextureAlias = "";
            cakeNpc.TintAlias = "";
            cakeNpc.Scale = 1.0f;
            cakeNpc.Animation = 1;
            cakeNpc.HideNamePlate = false;
            cakeNpc.IsInteractable = true;
            cakeNpc.CursorId = 5;

            var forwardDirection = Vector3.Transform(new Vector3(0, 0, 1), connection.Player.Rotation);
            var spawnPosition = new Vector4(
                connection.Player.Position.X + forwardDirection.X * 1.5f,
                connection.Player.Position.Y + forwardDirection.Y * 1.5f,
                connection.Player.Position.Z + forwardDirection.Z * 1.5f,
                connection.Player.Position.W
            );

            cakeNpc.Visible = true;
            cakeNpc.UpdatePosition(spawnPosition, connection.Player.Rotation);

            int[] bossAbilities = [4370, 4371, 4372, 4373];

            cakeNpc.InteractAction = (interactingPlayer) =>
            {
                int abilityId = bossAbilities[Random.Shared.Next(bossAbilities.Length)];

                if (_resourceManager.Consumables.Transformations.TryGetValue(abilityId, out var transform))
                    ApplyTransform(interactingPlayer, transform.ModelId, transform.DurationMs);
            };

            var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = cakeNpc.Guid,
                CompositeEffectId = 21,
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

            Task.Delay(60_000).ContinueWith(_ =>
            {
                try
                {
                    var removePacket = new PlayerUpdatePacketRemovePlayerGracefully
                    {
                        Guid = capturedNpc.Guid,
                        Animate = false,
                        Delay = 0,
                        EffectDelay = 0,
                        CompositeEffectId = 21,
                        Duration = 500
                    };

                    var players = startingZone.Players;
                    if (players is not null)
                    {
                        foreach (var player in players)
                            player.SendTunneled(removePacket);
                    }

                    capturedNpc.Dispose();
                }
                catch { }
            });
        }
        catch { }
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

            var leftDirection = System.Numerics.Vector3.Transform(
                new System.Numerics.Vector3(-1, 0, 0),
                connection.Player.Rotation
            );
            var spawnOffset = leftDirection * 2.0f;
            var spawnPosition = new System.Numerics.Vector4(
                connection.Player.Position.X + spawnOffset.X,
                connection.Player.Position.Y + spawnOffset.Y,
                connection.Player.Position.Z + spawnOffset.Z,
                connection.Player.Position.W
            );

            boomboxNpc.UpdatePosition(spawnPosition, connection.Player.Rotation);
            boomboxNpc.Visible = true;

            var addNpcPacket = boomboxNpc.GetAddNpcPacket();

            const float BoomboxRangeInMeters = 15.0f; // 2 meters range

            var nearbyPlayers = new List<Game.Entities.Player> { connection.Player };

            foreach (var player in connection.Player.VisiblePlayers.Values)
            {
                float distance = Vector3.Distance(
                    new Vector3(player.Position.X, player.Position.Y, player.Position.Z),
                    new Vector3(spawnPosition.X, spawnPosition.Y, spawnPosition.Z)
                );

                if (distance <= BoomboxRangeInMeters)
                {
                    nearbyPlayers.Add(player);
                }
            }

            var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
            {
                Guid = boomboxNpc.Guid,
                Unknown2 = 0,
                CompositeEffectId = 21, // PFX_smoke_black_explosion
                Unknown4 = 0,
                EffectDelay = 0,
                Position = spawnPosition,
                Clear = false
            };

            foreach (var player in nearbyPlayers)
            {
                player.SendTunneled(poofEffect);
                player.SendTunneled(addNpcPacket);
            }

            var capturedNpc = boomboxNpc;

            StartBoomboxDancing(startingZone, spawnPosition, danceSequence, 60_000);

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

                    var players = startingZone.Players;
                    if (players is not null)
                    {
                        foreach (var player in players)
                            player.SendTunneled(removePacket);
                    }

                    capturedNpc.Dispose();
                }
                catch { }
            });
        }
        catch { }
    }

    private static void StartBoomboxDancing(Game.Zones.StartingZone zone, Vector4 boomboxPosition, int[] danceSequence, int durationMs)
    {
        const float BoomboxRangeInMeters = 15.0f;

        var danceInterval = 3000; // Send dance command every 3 seconds
        var iterations = durationMs / danceInterval;

        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {

                int sequenceIndex = 0; // Track which animation in the sequence we're on

                for (int i = 0; i < iterations; i++)
                {
                    await System.Threading.Tasks.Task.Delay(danceInterval);

                    int currentQuickChatId = danceSequence[sequenceIndex];
                    sequenceIndex = (sequenceIndex + 1) % danceSequence.Length; // Cycle back to 0 after reaching end

                    var allPlayers = zone.Players?.ToList() ?? new List<Game.Entities.Player>();
                    var playersInRange = allPlayers.Where(p =>
                    {
                        float distance = Vector3.Distance(
                            new Vector3(p.Position.X, p.Position.Y, p.Position.Z),
                            new Vector3(boomboxPosition.X, boomboxPosition.Y, boomboxPosition.Z)
                        );
                        return distance <= BoomboxRangeInMeters;
                    }).ToList();

                    foreach (var player in playersInRange)
                    {
                        try
                        {
                            var quickChatPacket = new QuickChatSendChatToChannelPacket
                            {
                                Id = currentQuickChatId,
                                Guid = player.Guid,
                                Name = player.Name ?? new Packet.Common.NameData(),
                                Channel = Packet.Common.Chat.ChatChannel.WorldArea,
                                AreaNameId = 0,
                                GuildGuid = 0
                            };

                            player.SendTunneled(quickChatPacket);

                            foreach (var visiblePlayer in player.VisiblePlayers.Values)
                            {
                                visiblePlayer.SendTunneled(quickChatPacket);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        });
    }

    private static void SendFailure(GatewayConnection connection, int stringId)
    {
        var abilityPacketFailed = new AbilityPacketFailed
        {
            StringId = stringId
        };

        connection.SendTunneled(abilityPacketFailed);
    }

    internal static void ApplyTransform(GatewayConnection connection, int temporaryAppearance, int durationMs)
        => connection.Player.ApplyTemporaryAppearance(temporaryAppearance, durationMs);

    internal static void ApplyTransform(Game.Entities.Player player, int temporaryAppearance, int durationMs)
        => player.ApplyTemporaryAppearance(temporaryAppearance, durationMs);

    internal static void RemoveTransform(GatewayConnection connection)
        => connection.Player.RemoveTemporaryAppearance();
}
