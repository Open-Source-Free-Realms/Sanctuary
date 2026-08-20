using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Game.Housing;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketRequestPlayerHousesHandler
{
    private static ILogger _logger = null!;
    private static IHouseManager _houseManager = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketRequestPlayerHousesHandler));
        _houseManager = serviceProvider.GetRequiredService<IHouseManager>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketRequestPlayerHouses.TryDeserialize(data, out _))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketRequestPlayerHouses));
            return false;
        }

        _logger.LogTrace("Received {name} packet.", nameof(ClientHousingPacketRequestPlayerHouses));

        SendHouseList(connection);
        return true;
    }

    public static void SendHouseList(GatewayConnection connection)
    {
        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var houses = _houseManager.GetOwnedHouses(characterId);
        var packet = new HousingPacketInstanceList
        {
            PlayerGuid = connection.Player.Guid
        };

        foreach (var house in houses)
        {
            if (!_resourceManager.Zones.TryGetValue(house.ZoneDefinitionId, out var definition) ||
                definition is not HousingZoneDefinition housingDefinition)
            {
                continue;
            }

            packet.Instances.Add(new PlayerHousingInstanceInfo
            {
                OwnerGuid = connection.Player.Guid,
                InstanceGuid = GuidHelper.GetHouseGuid(house.Id),
                NameId = housingDefinition.NameId,
                OwnerName = connection.Player.Name.FullName,
                HouseName = housingDefinition.DisplayName,
                IconId = housingDefinition.IconId,
                LastVisited = house.Created,
                IsFloraAllowed = true,
                Description = string.Empty,
                KeywordList = string.Empty,
                Unknown21 = string.Empty,
                WhenCreated = house.Created
            });
        }

        connection.SendTunneled(packet);
    }
}
