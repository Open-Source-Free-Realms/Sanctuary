using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GatewayLoginReplyHandler
{
    private static ILogger _logger = null!;
    private static IHostApplicationLifetime _hostApplicationLifetime = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GatewayLoginReplyHandler));

        _hostApplicationLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!GatewayLoginReply.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GatewayLoginReply));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GatewayLoginReply), packet);

        if (!packet.Success)
        {
            _logger.LogCritical("Failed to login to Login Server.");

            _hostApplicationLifetime.StopApplication();
        }

        return true;
    }
}