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
            ClientHousingPacketPickupAllFixtures.OpCode => ClientHousingPacketPickupAllFixturesHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketSaveFixture.OpCode => ClientHousingPacketSaveFixtureHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketSetEditMode.OpCode => ClientHousingPacketSetEditModeHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketChangeHouseName.OpCode => ClientHousingPacketChangeHouseNameHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketLeaveHouse.OpCode => ClientHousingPacketLeaveHouseHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketToggleLocked.OpCode => ClientHousingPacketToggleLockedHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketToggleFloraAllowed.OpCode => ClientHousingPacketToggleFloraAllowedHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketTogglePetAutospawn.OpCode => ClientHousingPacketTogglePetAutospawnHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketRequestPlayerHouses.OpCode => ClientHousingPacketRequestPlayerHousesHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketRequestName.OpCode => ClientHousingPacketRequestNameHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketEnterRequest.OpCode => ClientHousingPacketEnterRequestHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketRequestVisitToNeighbor.OpCode => ClientHousingPacketRequestVisitToNeighborHandler.HandlePacket(connection, reader.Span),
            ClientHousingPacketLockHouse.OpCode => ClientHousingPacketLockHouseHandler.HandlePacket(connection, reader.Span),
            _ => LogUnhandled(opCode, reader)
        };
    }

    private static bool LogUnhandled(short opCode, PacketReader reader)
    {
        // Upkeep, invites, previews, grants, explosions and fixture customization aren't implemented yet.
        _logger.LogDebug("Unhandled housing packet {opCode}. ( Data: {data} )", opCode, Convert.ToHexString(reader.Span));

        return true;
    }
}