using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseCombatPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseCombatPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        var data = reader.Span.ToArray();
        reader.Reset();

        var packet = new BaseCombatPacket();
        if (!packet.TryRead(ref reader))
        {
            _logger.LogWarning(
                "{connection} failed to parse combat packet. ( Length: {length}, Data: {data} )",
                connection,
                data.Length,
                Convert.ToHexString(data));

            return false;
        }

        _logger.LogInformation(
            "{connection} received combat packet. ( SubOpCode: {subOpCode}, Length: {length}, Data: {data} )",
            connection,
            packet.SubOpCode,
            data.Length,
            Convert.ToHexString(data));

        return true;
    }
}
