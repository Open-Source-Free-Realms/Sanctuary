using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AbilityPacketDetonateProjectileHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(AbilityPacketDetonateProjectileHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!AbilityPacketDetonateProjectile.TryDeserialize(data, out var abilityPacketDetonateProjectile))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(AbilityPacketDetonateProjectile));
            return false;
        }

        var handled = connection.Player.Projectiles.HandleHit(connection.Player, abilityPacketDetonateProjectile);
        _logger.LogTrace("Projectile hit. Player: {guid}, target: {target}, animation: {animation}, handled: {handled}.",
            connection.Player.Guid, abilityPacketDetonateProjectile.Guid, abilityPacketDetonateProjectile.CompositeEffectId, handled);
        return true;
    }
}
