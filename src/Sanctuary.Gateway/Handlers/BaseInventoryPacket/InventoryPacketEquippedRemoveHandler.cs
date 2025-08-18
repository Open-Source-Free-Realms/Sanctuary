using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketEquippedRemoveHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketEquippedRemoveHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketEquippedRemove.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketEquippedRemove));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketEquippedRemove), packet);


        var profile = connection.Player.Profiles.SingleOrDefault(x => x.Id == packet.ProfileId);

        if (profile is null)
        {
            _logger.LogWarning("Invalid player profile id. {id}", packet.ProfileId);
            return true;
        }

        if (!profile.Items.TryGetValue(packet.Slot, out var profileItem))
            return true;

        profile.Items.Remove(packet.Slot);

        return true;
    }
}