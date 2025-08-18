using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketTunneledClientWorldPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketTunneledClientWorldPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketTunneledClientPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketTunneledClientPacket));
            return false;
        }

        var reader = new PacketReader(packet.Payload);

        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(data));
            return false;
        }

        var handled = opCode switch
        {
            PacketSetLocale.OpCode => PacketSetLocaleHandler.HandlePacket(connection, packet.Payload),
            BaseHousingPacket.OpCode => BaseHousingPacketHandler.HandlePacket(connection, reader),
            WallOfDataBasePacket.OpCode => WallOfDataBasePacketHandler.HandlePacket(connection, reader),
            _ => false
        };

#if DEBUG
        if (!handled)
        {
            reader.Reset();
            System.Diagnostics.Debug.WriteLine(reader.ReadTunneledPacketName(), "TunneledClientWorld");
        }
#endif

        return handled;
    }
}