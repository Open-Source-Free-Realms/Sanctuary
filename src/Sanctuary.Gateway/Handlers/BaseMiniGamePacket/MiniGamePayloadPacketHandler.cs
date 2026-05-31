using System;
using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class MiniGamePayloadPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(MiniGamePayloadPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!MiniGamePayloadPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(MiniGamePayloadPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(MiniGamePayloadPacket), packet);

        // Mining Practice
        if (packet.StateId == 1113)
        {
            var message = Encoding.UTF8.GetString(packet.Payload).TrimEnd('\0');

            Debug.WriteLine(message);

            var args = message.Split('\t');

            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "OnConnectMsg":
                        {
                            var miniGamePayloadPacket2 = new MiniGamePayloadPacket
                            {
                                Payload = Encoding.UTF8.GetBytes("OnServerReadyMsg\0")
                            };

                            connection.SendTunneled(miniGamePayloadPacket2);
                        }
                        break;

                    default:
                        Debug.WriteLine(message);
                        break;
                }
            }
        }

        return true;
    }
}