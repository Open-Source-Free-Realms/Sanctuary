using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Gateway.Services;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketClientIsReadyHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketClientIsReadyHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(PacketClientIsReady));

        try
        {
            connection.Player.Zone.OnClientIsReady(connection.Player);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Client ready initialization failed. PlayerGuid: {playerGuid}, CharacterName: {characterName}, Zone: {zone}, GuildGuid: {guildGuid}",
                connection.Player.Guid,
                connection.Player.Name,
                connection.Player.Zone.Name,
                connection.Player.GuildData?.Guid);

            connection.Disconnect();
        }

        return true;
    }
}
