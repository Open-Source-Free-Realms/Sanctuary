using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketMountSpawnHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketMountSpawnHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketMountSpawn.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketMountSpawn));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketMountSpawn), packet);

        var mountInfo = connection.Player.Mounts.SingleOrDefault(x => x.Id == packet.Id);

        if (mountInfo is null)
            return true;

        if (!_resourceManager.Mounts.TryGetValue(packet.Id, out var mountDefinition))
            return true;

        if (!connection.Player.Zone.TryCreateMount(connection.Player, mountDefinition, out var mount))
            return true;

        mount.Position = connection.Player.Position;
        mount.Rotation = connection.Player.Rotation;

        mount.Visible = true;

        mount.NameId = mountDefinition.NameId;
        mount.ModelId = mountDefinition.ModelId;

        mount.Scale = 1f;
        mount.Disposition = 1;

        mount.HideNamePlate = true;

        mount.ImageSetId = mountDefinition.ImageSetId;

        connection.Player.Mount = mount;

        connection.Player.OnEntityAdd(mount);

        foreach (var visibleEntity in connection.Player.VisibleEntities)
        {
            visibleEntity.Value.OnEntityAdd(mount);
        }

        var packetMountResponse = new PacketMountResponse();

        packetMountResponse.RiderGuid = mount.Rider.Guid;
        packetMountResponse.MountGuid = mount.Guid;

        packetMountResponse.Seat = 0;

        packetMountResponse.QueuePosition = 1;

        packetMountResponse.Unknown = 1;

        packetMountResponse.CompositeEffectId = 46; // PFX_Teleport_Flash

        packetMountResponse.NameVerticalOffset = mountDefinition.NameVerticalOffset;

        connection.Player.SendTunneledToVisible(packetMountResponse, true);

        var mountStats = mountDefinition.Stats;

        // TODO: Check if the mount was upgraded
        if (mountDefinition.IsUpgradable /* && dbMount.IsUpgraded */)
        {
            mountStats.MaxMovementSpeed = 12.5f;

            mountStats.GlideDefaultForwardSpeed = 8f;
            mountStats.GlideMinForwardSpeed = 2f;
            mountStats.GlideMaxForwardSpeed = 18f;
            mountStats.GlideFallTime = 0.75f;
            mountStats.GlideFallSpeed = 4f;
            mountStats.GlideEnabled = 1;
            mountStats.GlideAccel = 4f;
        }

        connection.Player.UpdateCharacterStats(
            CharacterStats.MaxMovementSpeed.Set(mountStats.MaxMovementSpeed),

            CharacterStats.GlideDefaultForwardSpeed.Set(mountStats.GlideDefaultForwardSpeed),
            CharacterStats.GlideMinForwardSpeed.Set(mountStats.GlideMinForwardSpeed),
            CharacterStats.GlideMaxForwardSpeed.Set(mountStats.GlideMaxForwardSpeed),
            CharacterStats.GlideFallTime.Set(mountStats.GlideFallTime),
            CharacterStats.GlideFallSpeed.Set(mountStats.GlideFallSpeed),
            CharacterStats.GlideEnabled.Set(mountStats.GlideEnabled),
            CharacterStats.GlideAccel.Set(mountStats.GlideAccel),

            CharacterStats.JumpHeight.Set(mountStats.JumpHeight));

        return true;
    }
}