using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseActivityServicePacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseActivityServicePacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        var data = reader.Span.ToArray();

        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read activity-service opcode. ( Data: {data} )", Convert.ToHexString(data));
            return false;
        }

        if (opCode != BaseActivityServicePacket.OpCode)
        {
            _logger.LogWarning(
                "{connection} received unexpected activity-service opcode. ( OpCode: {opCode}, Length: {length}, Data: {data} )",
                connection,
                opCode,
                data.Length,
                Convert.ToHexString(data));

            return false;
        }

        if (!reader.TryRead(out byte serviceSubOpCode))
        {
            _logger.LogWarning(
                "{connection} received truncated activity-service packet. ( Length: {length}, Data: {data} )",
                connection,
                data.Length,
                Convert.ToHexString(data));

            return false;
        }

        byte? activitySubOpCode = null;
        if (serviceSubOpCode == 1 && reader.TryRead(out byte value))
            activitySubOpCode = value;

        _logger.LogInformation(
            "{connection} received activity-service packet. ( ServiceSubOpCode: {serviceSubOpCode}, ActivitySubOpCode: {activitySubOpCode}, RemainingLength: {remainingLength}, RemainingData: {remainingData}, Length: {length}, Data: {data} )",
            connection,
            serviceSubOpCode,
            activitySubOpCode,
            reader.RemainingLength,
            Convert.ToHexString(reader.RemainingSpan),
            data.Length,
            Convert.ToHexString(data));

        return true;
    }
}
