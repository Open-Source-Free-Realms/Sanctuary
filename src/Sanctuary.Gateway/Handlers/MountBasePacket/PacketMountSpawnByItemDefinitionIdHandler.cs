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

        var mountInfo = connection.Player.Mounts.LastOrDefault(x => x.Definition == clientItemDefinition.Param1);

        if (mountInfo is null)
            return true;

        PacketMountSpawnHandler.SpawnMount(connection, mountInfo);

        return true;
    }
}