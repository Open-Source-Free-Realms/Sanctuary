using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketTunneledClientPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketTunneledClientPacketHandler));
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
            PacketClientFinishedLoading.OpCode => PacketClientFinishedLoadingHandler.HandlePacket(connection),
            PacketClientIsReady.OpCode => PacketClientIsReadyHandler.HandlePacket(connection),
            BaseChatPacket.OpCode => BaseChatPacketHandler.HandlePacket(connection, reader),
            BaseCommandPacket.OpCode => BaseCommandPacketHandler.HandlePacket(connection, reader),
            BasePlayerUpdatePacket.OpCode => BasePlayerUpdatePacketHandler.HandlePacket(connection, reader),
            BaseInventoryPacket.OpCode => BaseInventoryPacketHandler.HandlePacket(connection, reader),
            PacketGameTimeSync.OpCode => PacketGameTimeSyncHandler.HandlePacket(connection, packet.Payload),
            BaseQuickChatPacket.OpCode => BaseQuickChatPacketHandler.HandlePacket(connection, reader),
            PacketZoneTeleportRequest.OpCode => PacketZoneTeleportRequestHandler.HandlePacket(connection, packet.Payload),
            PacketClientMetrics.OpCode => PacketClientMetricsHandler.HandlePacket(connection, packet.Payload),
            PacketClientLog.OpCode => PacketClientLogHandler.HandlePacket(connection, packet.Payload),
            PacketZoneSafeTeleportRequest.OpCode => PacketZoneSafeTeleportRequestHandler.HandlePacket(connection, packet.Payload),
            PlayerUpdatePacketUpdatePosition.OpCode => PlayerUpdatePacketUpdatePositionHandler.HandlePacket(connection, packet.Payload),
            PlayerUpdatePacketCameraUpdate.OpCode => PlayerUpdatePacketCameraUpdateHandler.HandlePacket(connection, packet.Payload),
            BaseHousingPacket.OpCode => BaseHousingPacketHandler.HandlePacket(connection, reader),
            BasePlayerTitlePacket.OpCode => BasePlayerTitlePacketHandler.HandlePacket(connection, reader),
            PlayerUpdatePacketJump.OpCode => PlayerUpdatePacketJumpHandler.HandlePacket(connection, packet.Payload),
            MountBasePacket.OpCode => MountBasePacketHandler.HandlePacket(connection, reader),
            PacketClientInitializationDetails.OpCode => PacketClientInitializationDetailsHandler.HandlePacket(connection, packet.Payload),
            BaseNameChangePacket.OpCode => BaseNameChangePacketHandler.HandlePacket(connection, reader),
            _ => false
        };

#if DEBUG
        if (!handled)
        {
            reader.Reset();
            System.Diagnostics.Debug.WriteLine(reader.ReadTunneledPacketName(), "TunneledClient");
        }
#endif

        return handled;
    }
}