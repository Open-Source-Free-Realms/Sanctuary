using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseHousingPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseHousingPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            ClientHousingPacketPlaceFixtureRequest.OpCode => ClientHousingPacketPlaceFixtureRequestHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketPlaceFixture.OpCode => ClientHousingPacketPlaceFixtureHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketPickupFixture.OpCode => ClientHousingPacketPickupFixtureHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketPickupAllFixturesHandler.OpCode => ClientHousingPacketPickupAllFixturesHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketSaveFixture.OpCode => ClientHousingPacketSaveFixtureHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketSetEditMode.OpCode => ClientHousingPacketSetEditModeHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketLeaveHouse.OpCode => ClientHousingPacketLeaveHouseHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketRequestPlayerHouses.OpCode => ClientHousingPacketRequestPlayerHousesHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketEnterRequest.OpCode => ClientHousingPacketEnterRequestHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketRequestGrantHandler.OpCode => ClientHousingPacketRequestGrantHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketApplyCustomizationToFixtureGroupAndType.OpCode => ClientHousingPacketApplyCustomizationToFixtureGroupAndTypeHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType.OpCode => ClientHousingPacketRemoveCustomizationFromFixtureGroupAndTypeHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}
