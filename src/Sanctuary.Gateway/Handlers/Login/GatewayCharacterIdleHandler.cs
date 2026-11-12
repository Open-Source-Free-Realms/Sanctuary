using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GatewayCharacterIdleHandler
{
    private static ILogger _logger = null!;
    private static GatewayServer _gatewayServer = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GatewayCharacterIdleHandler));

        _gatewayServer = serviceProvider.GetRequiredService<GatewayServer>();
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!GatewayCharacterIdleRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GatewayCharacterIdleRequest));
            return false;
        }

        var found = _gatewayServer.TryGetCharacterIdle(packet.Id, out var idleMilliseconds);

        _logger.LogInformation("Character {id} last packet was {idle} ms ago. Found={found}.", packet.Id, idleMilliseconds, found);

        connection.Send(new GatewayCharacterIdleReply
        {
            RequestId = packet.RequestId,
            Found = found,
            IdleMilliseconds = idleMilliseconds
        });

        return true;
    }
}
