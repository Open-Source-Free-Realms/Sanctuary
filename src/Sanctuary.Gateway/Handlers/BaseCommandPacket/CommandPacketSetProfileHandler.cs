using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketSetProfileHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketSetProfileHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketSetProfile.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketSetProfile));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketSetProfile), packet);

        var profile = connection.Player.Profiles.FirstOrDefault(x => x.Id == packet.Id);

        if (profile is null)
            return true;

        connection.Player.ActiveProfileId = packet.Id;

        var clientUpdatePacketActivateProfile = new ClientUpdatePacketActivateProfile();

        using var packetWriter = new PacketWriter();

        profile.Serialize(packetWriter);

        clientUpdatePacketActivateProfile.Payload = packetWriter.Buffer;

        clientUpdatePacketActivateProfile.Attachments = connection.Player.GetAttachments();

        clientUpdatePacketActivateProfile.Animation = 3001; // emo_outfit_all
        clientUpdatePacketActivateProfile.CompositeEffect = 4005; // PFX_Job_Swirl

        connection.SendTunneled(clientUpdatePacketActivateProfile);

        var playerUpdatePacketEquippedItemsChange = new PlayerUpdatePacketEquippedItemsChange();

        playerUpdatePacketEquippedItemsChange.Guid = connection.Player.Guid;

        playerUpdatePacketEquippedItemsChange.Attachments = clientUpdatePacketActivateProfile.Attachments;

        connection.Player.SendTunneledToVisible(playerUpdatePacketEquippedItemsChange);

        return true;
    }
}