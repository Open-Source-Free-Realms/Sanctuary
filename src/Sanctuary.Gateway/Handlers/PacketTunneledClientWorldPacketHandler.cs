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
        if (!PacketTunneledClientWorldPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketTunneledClientWorldPacket));
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
            BaseCommandPacket.OpCode => BaseCommandPacketHandler.HandlePacket(connection, reader),
            BaseCombatPacket.OpCode => BaseCombatPacketHandler.HandlePacket(connection, reader),
            BaseAbilityPacket.OpCode => BaseAbilityPacketHandler.HandlePacket(connection, reader),
            PacketWorldTeleportRequest.OpCode => PacketWorldTeleportRequestHandler.HandlePacket(connection, packet.Payload),
            PacketBaseInGamePurchase.OpCode => PacketBaseInGamePurchaseHandler.HandlePacket(connection, reader),
            PacketSetLocale.OpCode => PacketSetLocaleHandler.HandlePacket(connection, packet.Payload),
            BaseLobbyGameDefinitionPacket.OpCode => BaseLobbyGameDefinitionPacketHandler.HandlePacket(connection, reader),
            BaseHousingPacket.OpCode => BaseHousingPacketHandler.HandlePacket(connection, reader),
            BaseGuildPacket.OpCode => BaseGuildPacketHandler.HandlePacket(connection, reader),
            BaseFotomatPacket.OpCode => BaseFotomatPacketHandler.HandlePacket(connection, reader),
            WallOfDataBasePacket.OpCode => WallOfDataBasePacketHandler.HandlePacket(connection, reader),
            _ => false
        };

        if (!handled)
        {
            var packetName = "Unknown";

            try
            {
                reader.Reset();
                packetName = reader.ReadTunneledPacketName();
            }
            catch
            {
                // Keep the original failure visible below.
            }

            _logger.LogDebug(
                "{connection} received unhandled TunneledClientWorld packet. ( Packet: {packetName}, Data: {data} )",
                connection,
                packetName,
                Convert.ToHexString(packet.Payload));
        }

        return handled;
    }
}
