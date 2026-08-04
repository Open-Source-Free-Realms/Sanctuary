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

// Opcode 98 - the "Take Me There" path family. On a ClientPathRequestPacket (sub 1, sent when the button
// is clicked) we reply with a ClientPathReplyPacket (sub 2) whose waypoint list the client turns into the
// green breadcrumb trail + auto-walk. The path runs from the client's start position to the tracked
// quest's target NPC (falling back to the client-provided end point).
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

    // "Arrived" radius for auto-walk session cancellation - close enough that the player would naturally
    // interact with the target NPC, so keeps resending an auto-walk becomes pointless/annoying.
    private const float ArrivalDistance = 8f;

    // How far the resolved destination can drift between refreshes before we treat it as a genuinely
    // DIFFERENT objective (a manual re-track) rather than the same tracked NPC/kill-goal wandering a
    // little. A real goal switch shows up as one big jump on the very next refresh after it happens.
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

        // The reply's ResultType routes it to a different client controller: 1 = the breadcrumb trail
        // (renders the green line), 2 = the character auto-move (pushes the path into the ProxiedCharacter's
        // movement so it actually walks).
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
            // Passive refresh mid-session (the client sends these automatically as the player moves).
            // Only keep resending the walk while it's still the SAME objective and we haven't arrived -
            // otherwise this is exactly the "wander off on its own" bug the Mode-2-only gate used to
            // prevent, just re-triggered continuously instead of once.
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

        _logger.LogInformation("[Path] {kind} for {name}: {a} -> {b} ({n} nodes){active}",
            request.Mode == 2 ? "Take-Me-There walk" : "trail refresh", player.Name, request.Start, destination, path.Count,
            player.TakeMeThereActive ? " [session active]" : "");
        return true;
    }

    // Builds the path the client walks, via the zone's Pathfinder<MapNode> (Sanctuary.Game.Pathfinding,
    // real .map waypoint data - see Player.TryGetPath). Falls back to a straight line when the zone has
    // no .map file, or the graph can't connect start/destination (disconnected components).
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
