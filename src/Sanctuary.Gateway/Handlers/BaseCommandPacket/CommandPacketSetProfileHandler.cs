using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Helpers;
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
        if (!CommandPacketSetProfile.TryDeserialize(data, out var commandPacketSetProfile))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketSetProfile));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketSetProfile), commandPacketSetProfile);

        var profile = connection.Player.Profiles.FirstOrDefault(x => x.Id == commandPacketSetProfile.Id);

        if (profile is null)
            return true;

        connection.Player.ActivateProfile(profile);

        if (connection.Player.MiniGameUiSession is not null)
            MiniGameHelper.RefreshDetails(connection.Player, connection.Player.MiniGameUiSession);

        return true;
    }
}
