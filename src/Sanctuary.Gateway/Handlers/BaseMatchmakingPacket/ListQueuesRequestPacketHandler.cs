using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ListQueuesRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ListQueuesRequestPacketHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ListQueuesRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ListQueuesRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet.", nameof(ListQueuesRequestPacket));

        var listQueuesResponsePacket = new ListQueuesResponsePacket();

        listQueuesResponsePacket.Guid = packet.Guid;

        var queues = new MatchmakingQueueDefinition[]
        {
            new MatchmakingQueueDefinition
            {
                // Pirate's Plunder
                Id = 5,
                NameId = 427834,
                MatchType = 13,
                MinPlayers = 1,
                MaxPlayers = 5,
                MinTeams = 1,
                MaxTeams = 1,
                MaxGameStartDelay = 30,
                Param1 = 0,
                Param2 = 0,
                Param3 = 0,
                Param4 = 0,
                Param5 = 420998,
                Param6 = 30434,
                Param7 = 1,
                EncounterDescriptionId = 420998,
                EncounterIcon = 30434,
                Unknown = 0,
                Unknown2 = 26,
                MemberOnly = true,
                Unknown3 = true,
            },
            new MatchmakingQueueDefinition
            {
                // Soccer
                Id = 11,
                NameId = 31030,
                MatchType = 12,
                MinPlayers = 1,
                MaxPlayers = 3,
                MinTeams = 1,
                MaxTeams = 2,
                MaxGameStartDelay = 120,
                Param1 = 0,
                Param2 = 3,
                Param3 = 0,
                Param4 = 8,
                Param5 = 37947,
                Param6 = 20992,
                Param7 = 4,
                EncounterDescriptionId = 37947,
                EncounterIcon = 20992,
                Unknown = 8,
                Unknown2 = 0,
                MemberOnly = false,
                Unknown3 = true,
            },
            new MatchmakingQueueDefinition
            {
                // Kart Racing
                Id = 21,
                NameId = 426304,
                MatchType = 10,
                MinPlayers = 1,
                MaxPlayers = 5,
                MinTeams = 1,
                MaxTeams = 1,
                MaxGameStartDelay = 120,
                Param1 = 1,
                Param2 = 3,
                Param3 = 0,
                Param4 = 5,
                Param5 = 407515,
                Param6 = 20991,
                Param7 = 2,
                EncounterDescriptionId = 407515,
                EncounterIcon = 20991,
                Unknown = 5,
                Unknown2 = 0,
                MemberOnly = false,
                Unknown3 = true,
            },
            new MatchmakingQueueDefinition
            {
                // Demo Derby
                Id = 31,
                NameId = 382789,
                MatchType = 11,
                MinPlayers = 1,
                MaxPlayers = 5,
                MinTeams = 1,
                MaxTeams = 1,
                MaxGameStartDelay = 120,
                Param1 = 0,
                Param2 = 0,
                Param3 = 0,
                Param4 = 7,
                Param5 = 37942,
                Param6 = 9871,
                Param7 = 3,
                EncounterDescriptionId = 37942,
                EncounterIcon = 9871,
                Unknown = 7,
                Unknown2 = 0,
                MemberOnly = false,
                Unknown3 = true,
            },
            new MatchmakingQueueDefinition
            {
                // Snowball Fighting
                Id = 51,
                NameId = 419545,
                MatchType = 4,
                MinPlayers = 4,
                MaxPlayers = 4,
                MinTeams = 1,
                MaxTeams = 1,
                MaxGameStartDelay = 0,
                Param1 = 369,
                Param2 = 0,
                Param3 = 0,
                Param4 = 0,
                Param5 = 0,
                Param6 = 0,
                Param7 = 0,
                EncounterDescriptionId = 419546,
                EncounterIcon = 282,
                Unknown = 1,
                Unknown2 = 0,
                MemberOnly = false,
                Unknown3 = true,
            }
        };

        listQueuesResponsePacket.Queues.AddRange(queues);

        connection.SendTunneled(listQueuesResponsePacket);

        return true;
    }
}