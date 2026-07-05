using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.Combat;
using Sanctuary.Game.Entities;
using Sanctuary.Gateway.Combat;
using Sanctuary.Gateway.Services;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AbilityPacketClientRequestStartAbilityHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private const float NinjaCastSeconds = 0.5f;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(AbilityPacketClientRequestStartAbilityHandler));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!AbilityPacketClientRequestStartAbility.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(AbilityPacketClientRequestStartAbility));
            return false;
        }

        _logger.LogInformation(
            "{connection} requested ability start. ( ActionBarId: {actionBarId}, Slot: {slot}, Target: {target}, TargetGuid: {targetGuid}, Position: {position}, Length: {length}, Data: {data} )",
            connection,
            packet.Data.Id,
            packet.Data.Slot,
            packet.Target,
            packet.Guid,
            packet.Position,
            data.Length,
            Convert.ToHexString(data));

        if (packet.Data.Id == ItemActionBarService.ActionBarId)
            return ItemActionBarService.TryActivateItemSlot(connection, _resourceManager, packet.Data.Slot, _logger);

        if (!TrySendNinjaStartCasting(connection, packet))
            CombatBootstrap.TrySendStartCasting(connection, _resourceManager, packet.Data.Slot, packet.Guid, _logger);

        return true;
    }

    private static bool TrySendNinjaStartCasting(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet)
    {
        var player = connection.Player;

        if (player.ActiveProfileId != NinjaWeaponAbilities.NinjaProfileId ||
            NinjaWeaponAbilities.GetEquippedWeapon(player) is null)
        {
            return false;
        }

        var targetNpc = ResolveNinjaTarget(player, packet.Guid);
        var targetGuid = targetNpc?.Guid ?? (packet.Guid != 0 ? packet.Guid : player.Guid);
        var ability = NinjaWeaponAbilities.ResolveAbility(player, packet.Data.Slot);
        var animationOverride = CombatBootstrap.ConsumeDebugAnimationOverride(connection);

        var startCasting = new AbilityPacketStartCasting
        {
            Unknown = player.Guid,
            Unknown2 = targetGuid,
            CompositeEffectId = 0,
            Animation = animationOverride ?? ability.Animation,
            AbilityId = packet.Data.Slot + 1,
            ActionTime = NinjaCastSeconds,
            HasActionProgress = false,
        };

        connection.SendTunneled(startCasting);

        if (animationOverride is not null)
            CombatBootstrap.ScheduleAnimationProbeRecovery(connection, _logger, animationOverride.Value);

        if (targetNpc is null)
        {
            _logger.LogInformation("Ninja ability found no damageable target. ( Slot: {slot} )", packet.Data.Slot);
            return true;
        }

        _logger.LogInformation(
            "Resolved ninja ability. ( Slot: {slot}, Name: {name}, Damage: {damage}, Animation: {animation}, EffectId: {effectId}, TargetGuid: {targetGuid} )",
            packet.Data.Slot,
            ability.Name,
            ability.Damage,
            ability.Animation,
            ability.EffectId,
            targetNpc.Guid);

        ResolveNinjaDamageAfterCast(player, targetNpc, ability.Damage, ability.EffectId);
        return true;
    }

    private static Npc? ResolveNinjaTarget(Player player, ulong requestedTargetGuid)
    {
        if (requestedTargetGuid != 0 &&
            player.Zone.TryGetNpc(requestedTargetGuid, out var selected) &&
            selected.IsDamageable &&
            selected.IsAlive)
        {
            return selected;
        }

        return player.Zone.Npcs.FirstOrDefault(n => n.IsHostile && n.IsDamageable && n.IsAlive);
    }

    private static void ResolveNinjaDamageAfterCast(Player player, Npc target, int damage, int effectId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay((int)(NinjaCastSeconds * 1000));

                var killed = target.ApplyDamage(damage);
                var attackProcessed = new CombatPacketAttackProcessed
                {
                    Guid1 = player.Guid,
                    Guid2 = target.Guid,
                    Guid3 = target.Guid,
                    Int1 = damage,
                    Int2 = target.MaxHealth,
                    Int3 = effectId,
                    Bool1 = false,
                    Bool2 = false,
                    Int4 = 0,
                    Int5 = target.MaxHealth,
                };

                player.SendTunneledToVisible(attackProcessed, sendToSelf: true);

                _logger.LogInformation(
                    "Applied ninja ability damage. ( Target: {target}, TargetGuid: {targetGuid}, Damage: {damage}, Health: {health}/{maxHealth}, Killed: {killed} )",
                    target.Name,
                    target.Guid,
                    damage,
                    target.Health,
                    target.MaxHealth,
                    killed);

                if (killed)
                    target.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ninja ability damage resolution failed.");
            }
        });
    }
}

