using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildPaidRenameCheckRequestPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildPaidRenameCheckRequestPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildPaidRenameCheckRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildPaidRenameCheckRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildPaidRenameCheckRequestPacket), packet);

        var guildPaidRenameCheckReplyPacket = new GuildPaidRenameCheckReplyPacket
        {
            Guid = packet.Guid,
            Name = packet.Name,
            Result = 5
        };

        // TODO

        connection.SendTunneled(guildPaidRenameCheckReplyPacket);

        return true;
    }
}