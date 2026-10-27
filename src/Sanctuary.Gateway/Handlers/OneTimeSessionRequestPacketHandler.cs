using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// Answers one-time session requests so the client isn't left waiting (the daily wheel locked the UI without
/// one). What each type's session is used for isn't known yet, so every request is logged.
/// </summary>
[PacketHandler]
public static class OneTimeSessionRequestPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(OneTimeSessionRequestPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!OneTimeSessionRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}. ( Data: {data} )", nameof(OneTimeSessionRequestPacket), Convert.ToHexString(data));
            return false;
        }

        _logger.LogInformation("{player} requested a one-time session of type {type}.", connection.Player.Name.FullName, packet.Type);

        connection.SendTunneled(new OneTimeSessionResponsePacket
        {
            Success = true,
            Session = Guid.NewGuid().ToString("N"),
            Type = packet.Type
        });

        return true;
    }
}
