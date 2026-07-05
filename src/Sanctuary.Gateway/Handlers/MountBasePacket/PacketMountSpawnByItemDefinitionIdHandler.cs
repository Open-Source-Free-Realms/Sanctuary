using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketMountSpawnByItemDefinitionIdHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketMountSpawnByItemDefinitionIdHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketMountSpawnByItemDefinitionId.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketMountSpawnByItemDefinitionId));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketMountSpawnByItemDefinitionId), packet);

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(packet.ItemDefinitionId, out var clientItemDefinition))
            return true;

        var matchingMounts = connection.Player.Mounts
            .Where(x => x.Definition == clientItemDefinition.Param1)
            .ToList();

        if (matchingMounts.Count == 0)
            return true;

        if (matchingMounts.Count > 1)
        {
            _logger.LogWarning(
                "Multiple owned mounts matched spawn request. PlayerGuid={playerGuid}, ItemDefinitionId={itemDefinitionId}, MountDefinitionId={mountDefinitionId}, MatchCount={matchCount}",
                connection.Player.Guid,
                packet.ItemDefinitionId,
                clientItemDefinition.Param1,
                matchingMounts.Count);
        }

        PacketMountSpawnHandler.SpawnMount(connection, matchingMounts[0]);

        return true;
    }
}