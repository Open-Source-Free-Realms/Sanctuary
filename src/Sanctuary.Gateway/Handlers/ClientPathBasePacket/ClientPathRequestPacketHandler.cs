using System;
using System.Collections.Generic;
using System.Numerics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientPathRequestPacketHandler
{
    private static ILogger _logger = null!;

    private const float RaisedPointHeight = 1f;
    private const int MinRequestIntervalMs = 250;

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

        if (clientPathRequestPacket.Guid != connection.Player.Guid || Environment.TickCount64 < connection.Player.NextClientPathRequestAt)
            return true;

        connection.Player.NextClientPathRequestAt = Environment.TickCount64 + MinRequestIntervalMs;

        var clientPathReplyPacket = new ClientPathReplyPacket
        {
            Type = clientPathRequestPacket.Type,
            Id = clientPathRequestPacket.Id
        };

        var start = clientPathRequestPacket.FromPosition.ToVector3();
        var waypoints = new Queue<Vector3>();

        if (connection.Player.Zone.TryFindClientNavMeshPath(start, clientPathRequestPacket.ToPosition.ToVector3(), waypoints))
        {
            clientPathReplyPacket.Points.Add(Raise(start));

            while (waypoints.Count > 0)
            {
                var waypoint = waypoints.Dequeue();

                clientPathReplyPacket.Points.Add(waypoints.Count == 0 && clientPathReplyPacket.Points.Count == 1
                    ? Raise(waypoint)
                    : new Vector4(waypoint, 1f));
            }
        }

        connection.SendTunneled(clientPathReplyPacket);

        return true;
    }

    private static Vector4 Raise(Vector3 point) => new(point.X, point.Y + RaisedPointHeight, point.Z, 1f);
}
