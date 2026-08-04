using System;
using System.Collections.Generic;
using System.Numerics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

// Opcode 98 sub 1: "Take Me There" - replies with the waypoint path to the tracked quest's target NPC (or the client's end point).
[PacketHandler]
public static class ClientPathBasePacketHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientPathBasePacketHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        var fullBuffer = reader.Span;

        if (!reader.TryRead(out byte subOpCode))
            return false;

        return subOpCode switch
        {
            ClientPathRequestPacket.OpCode => HandlePathRequest(connection, fullBuffer),
            _ => false
        };
    }

    // Auto-walk cancels once the player is within this distance of the target.
    private const float ArrivalDistance = 8f;

    // Destination drift beyond this between refreshes is treated as a real objective change, not the same target wandering.
    private const float DestinationChangeThreshold = 40f;

    private static bool HandlePathRequest(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientPathRequestPacket.TryDeserialize(data, out var request))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientPathRequestPacket));
            return false;
        }

        var player = connection.Player;

        // Destination: the tracked quest's target NPC if we have one, otherwise the point the client asked for.
        var destination = request.End;
        if (_questManager.TryGetActiveObjectiveTarget(player, out var targetPosition))
            destination = new Vector4(targetPosition, 1f);

        var path = BuildPath(player, request.Start, destination);

        // ResultType 1 = breadcrumb trail render, 2 = character auto-move.
        var trail = new ClientPathReplyPacket { RequestId = request.RequestId, ResultType = 1 };
        trail.Path.AddRange(path);
        player.SendTunneled(trail);

        if (request.Mode == 2)
        {
            // A genuine "Take Me There" click starts (or restarts) an active auto-walk session.
            player.TakeMeThereActive = true;
            player.TakeMeThereDestination = destination;
        }
        else if (player.TakeMeThereActive)
        {
            // Passive refresh: only keep auto-walking while it's the same objective and not yet arrived.
            var start3 = new Vector3(request.Start.X, request.Start.Y, request.Start.Z);
            var dest3 = new Vector3(destination.X, destination.Y, destination.Z);
            var lastDest3 = new Vector3(player.TakeMeThereDestination.X, player.TakeMeThereDestination.Y, player.TakeMeThereDestination.Z);

            if (Vector3.Distance(start3, dest3) < ArrivalDistance || Vector3.Distance(lastDest3, dest3) > DestinationChangeThreshold)
                player.TakeMeThereActive = false;
            else
                player.TakeMeThereDestination = destination;
        }

        if (player.TakeMeThereActive)
        {
            var walk = new ClientPathReplyPacket { RequestId = request.RequestId, ResultType = 2 };
            walk.Path.AddRange(path);
            player.SendTunneled(walk);
        }

        return true;
    }

    // Uses the zone's .map waypoint graph (Player.TryGetPath); falls back to a straight line if unavailable.
    private static List<Vector4> BuildPath(Sanctuary.Game.Entities.Player player, Vector4 start, Vector4 destination)
    {
        var start3 = new Vector3(start.X, start.Y, start.Z);
        var dest3 = new Vector3(destination.X, destination.Y, destination.Z);

        var path = player.TryGetPath(start3, dest3);
        if (path is not null)
            return path;

        return new List<Vector4> { start, destination };
    }
}
