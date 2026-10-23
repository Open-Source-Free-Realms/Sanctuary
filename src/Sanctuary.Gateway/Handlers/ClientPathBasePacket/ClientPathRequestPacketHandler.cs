using System;
using System.Numerics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Pathing;
using Sanctuary.Game.Routines;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientPathRequestPacketHandler
{
    private static ILogger _logger = null!;

    private const float RaisedPointHeight = 1f;
    private const int SearchIterations = 4096;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientPathRequestPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientPathRequestPacket.TryDeserialize(data, out var clientPathRequestPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientPathRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientPathRequestPacket), clientPathRequestPacket);

        var player = connection.Player;

        if (clientPathRequestPacket.Guid != player.Guid)
            return true;

        // TODO: This is a temporary "hacky" fix for marshalling logic to the tick thread. This needs to be changed
        // to a proper marshalling system.
        player.Routines.SetRoutine($"ClientPath{clientPathRequestPacket.Type}", () =>
        {
            connection.SendTunneled(CreateReply(player.Zone, clientPathRequestPacket));
            return true;
        }, Cadence.Tick);

        return true;
    }

    private static ClientPathReplyPacket CreateReply(IZone zone, ClientPathRequestPacket clientPathRequestPacket)
    {
        var clientPathReplyPacket = new ClientPathReplyPacket
        {
            Type = clientPathRequestPacket.Type,
            Id = clientPathRequestPacket.Id
        };

        if (zone.NavMeshQuery is null)
            return clientPathReplyPacket;

        var pathfinder = new Pathfinder(zone.NavMeshQuery, zone.NavMeshFilter, SearchIterations);
        var start = clientPathRequestPacket.FromPosition.ToVector3();

        if (!pathfinder.TryFindPath(start, clientPathRequestPacket.ToPosition.ToVector3(), out var path))
            return clientPathReplyPacket;

        clientPathReplyPacket.Points.Add(Raise(start));

        foreach (var waypoint in path.Waypoints)
            clientPathReplyPacket.Points.Add(path.Waypoints.Count == 1 ? Raise(waypoint) : new Vector4(waypoint, 1f));

        return clientPathReplyPacket;
    }

    private static Vector4 Raise(Vector3 point) => new(point.X, point.Y + RaisedPointHeight, point.Z, 1f);
}
