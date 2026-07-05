using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Gateway.Services;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseHousingPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseHousingPacketHandler));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        try
        {
            return opCode switch
            {
                ClientHousingPacketPlaceFixtureRequest.OpCode => HandlePlaceFixtureRequest(connection, reader.Span),
                ClientHousingPacketPlaceFixture.OpCode => HandlePlaceFixture(connection, reader.Span),
                ClientHousingPacketPickupFixture.OpCode => HandlePickupFixture(connection, reader.Span),
                ClientHousingPacketSaveFixture.OpCode => HandleSaveFixture(connection, reader.Span),
                ClientHousingPacketSetEditMode.OpCode => ClientHousingPacketSetEditModeHandler.HandlePacket(connection, reader.Span),
                ClientHousingPacketEnterRequest.OpCode => ClientHousingPacketEnterRequestHandler.HandlePacket(connection, reader.Span),
                ClientHousingPacketRequestPlayerHouses.OpCode => HandleRequestPlayerHouses(connection, reader.Span),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Housing packet handling failed. PlayerGuid={playerGuid}, HousingOpCode={opCode}", connection.Player?.Guid, opCode);
            return true;
        }
    }

    private static bool HandleRequestPlayerHouses(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketRequestPlayerHouses.TryDeserialize(data, out _))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketRequestPlayerHouses));
            return false;
        }

        return HousingService.SendHousingUi(connection, _resourceManager, _logger);
    }

    private static bool HandlePlaceFixtureRequest(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketPlaceFixtureRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketPlaceFixtureRequest));
            return false;
        }

        HousingService.SendFixtureItemList(connection, _resourceManager);
        HousingService.SetEditMode(connection, true);

        _logger.LogTrace(
            "Accepted {packet}. PlayerGuid={playerGuid}, ItemDefinitionId={itemDefinitionId}",
            nameof(ClientHousingPacketPlaceFixtureRequest),
            connection.Player.Guid,
            packet.ItemDefinitionId);

        return true;
    }

    private static bool HandlePlaceFixture(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketPlaceFixture.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketPlaceFixture));
            return false;
        }

        return HousingService.PlaceFixture(
            connection,
            _resourceManager,
            packet.ItemDefinitionId,
            packet.FixtureGuid,
            packet.Position,
            packet.Rotation,
            _logger);
    }

    private static bool HandleSaveFixture(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketSaveFixture.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketSaveFixture));
            return false;
        }

        return HousingService.SaveFixture(connection, packet.FixtureGuid, packet.Position, packet.Rotation);
    }

    private static bool HandlePickupFixture(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketPickupFixture.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketPickupFixture));
            return false;
        }

        return HousingService.RemoveFixture(connection, packet.FixtureGuid);
    }
}
