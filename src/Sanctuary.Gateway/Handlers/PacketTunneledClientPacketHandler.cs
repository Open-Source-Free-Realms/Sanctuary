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
        if (!PacketTunneledClientPacket.TryDeserialize(data, out var packetTunneledClientPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketTunneledClientPacket));
            return false;
        }

        var reader = new PacketReader(packetTunneledClientPacket.Payload);

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
            BaseAbilityPacket.OpCode => BaseAbilityPacketHandler.HandlePacket(connection, reader),
            BaseInventoryPacket.OpCode => BaseInventoryPacketHandler.HandlePacket(connection, reader),
            PacketGameTimeSync.OpCode => PacketGameTimeSyncHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            PacketBaseInGamePurchase.OpCode => PacketBaseInGamePurchaseHandler.HandlePacket(connection, reader),
            AnnouncementDataSendPacket.OpCode => AnnouncementBasePacketHandler.HandlePacket(connection, reader),
            BaseQuickChatPacket.OpCode => BaseQuickChatPacketHandler.HandlePacket(connection, reader),
            PacketZoneTeleportRequest.OpCode => PacketZoneTeleportRequestHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            PacketClientMetrics.OpCode => PacketClientMetricsHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            PacketClientLog.OpCode => PacketClientLogHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            PacketZoneSafeTeleportRequest.OpCode => PacketZoneSafeTeleportRequestHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            PlayerUpdatePacketUpdatePosition.OpCode => PlayerUpdatePacketUpdatePositionHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            PlayerUpdatePacketCameraUpdate.OpCode => PlayerUpdatePacketCameraUpdateHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            BaseHousingPacket.OpCode => BaseHousingPacketHandler.HandlePacket(connection, reader),
            BasePlayerTitlePacket.OpCode => BasePlayerTitlePacketHandler.HandlePacket(connection, reader),
            PacketAddClientPortraitCrc.OpCode => PacketAddClientPortraitCrcHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            BaseFotomatPacket.OpCode => BaseFotomatPacketHandler.HandlePacket(connection, reader),
            PlayerUpdatePacketJump.OpCode => PlayerUpdatePacketJumpHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            BaseCoinStorePacket.OpCode => BaseCoinStorePacketHandler.HandlePacket(connection, reader),
            MountBasePacket.OpCode => MountBasePacketHandler.HandlePacket(connection, reader),
            PetBasePacket.OpCode => PetBasePacketHandler.HandlePacket(connection, reader),
            PacketClientInitializationDetails.OpCode => PacketClientInitializationDetailsHandler.HandlePacket(connection, packetTunneledClientPacket.Payload),
            BaseNameChangePacket.OpCode => BaseNameChangePacketHandler.HandlePacket(connection, reader),
            ClientPathBasePacket.OpCode => ClientPathBasePacketHandler.HandlePacket(connection, reader),
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
