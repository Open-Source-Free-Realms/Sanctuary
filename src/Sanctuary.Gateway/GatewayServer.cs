using System;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Configuration;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Gateway;

public class GatewayServer : UdpManager<GatewayConnection>
{
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;

    public GatewayServer(ILogger<GatewayServer> logger, IResourceManager resourceManager, UdpParams udpParams, IServiceProvider serviceProvider) : base(udpParams, serviceProvider)
    {
        _logger = logger;
        _resourceManager = resourceManager;

        // Run game callbacks after releasing the UDP connection lock. Zone ticks send
        // packets while holding zone locks, so inline callbacks can invert that order.
        EventQueuing = true;
    }

    public override bool OnConnectRequest(UdpConnection udpConnection)
    {
        _logger.LogInformation("{connection} connected.", udpConnection);

        return true;
    }

    public void OnStarted()
    {
        _resourceManager.Zones.CollectionChanged -= Zones_CollectionChanged;
        _resourceManager.Zones.CollectionChanged += Zones_CollectionChanged;
    }

    private void Zones_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
    }

    /// <summary>
    /// Drops the character's sessions if none has received a packet within <paramref name="idleThresholdMilliseconds"/>.
    /// Returns false, and drops nothing, while any of them is still live. A session on a loading screen counts as live.
    /// Otherwise returns true and calls <paramref name="onReleased"/> once every dropped session has saved and logged out
    /// (right away if there was none).
    /// </summary>
    public bool TryReleaseIdleCharacter(ulong characterId, uint idleThresholdMilliseconds, Action onReleased)
    {
        var sessions = ConnectionList
            .Where(x => x.Player is not null && GuidHelper.GetPlayerId(x.Player.Guid) == characterId)
            .ToList();

        // Login asks again every couple of seconds while it waits, so these checks log at Debug.
        foreach (var session in sessions)
        {
            // Already being dropped for an earlier request; login will see it gone on a later ask.
            if (session.OnReleased is not null)
                return false;

            // The client goes completely quiet while loading (up to ~25s when zoning), so silence there isn't a crash.
            if (session.Player.IsLoading)
            {
                _logger.LogDebug("Character {id} is still loading on {connection}, treating it as live.", characterId, session);
                return false;
            }

            var idle = session.MillisecondsSinceLastReceive();

            if (idle < idleThresholdMilliseconds)
            {
                _logger.LogDebug("Character {id} is still live on {connection} (last packet {idle} ms ago).", characterId, session, idle);
                return false;
            }
        }

        if (sessions.Count == 0)
        {
            onReleased();
            return true;
        }

        // Answer only after the last dropped session has saved, not when Disconnect is called.
        var remaining = sessions.Count;

        foreach (var session in sessions)
        {
            _logger.LogInformation("Dropping silent {connection} so character {id} can log in again.", session, characterId);

            session.OnReleased = () =>
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                    onReleased();
            };

            session.Disconnect();
        }

        return true;
    }

    public void OnStopping()
    {
        _resourceManager.Zones.CollectionChanged -= Zones_CollectionChanged;

        var packetNotice = new PacketWorldShutdownNotice();

        // Scheduled maintenance and updates.
        packetNotice.ReasonId = 418992;

        var packetTunneled = new PacketTunneledClientPacket();

        packetTunneled.Payload = packetNotice.Serialize();

        var packetData = packetTunneled.Serialize();

        foreach (var connection in ConnectionList)
        {
            connection.Send(UdpChannel.Reliable1, packetData);
        }
    }

    public override void Dispose()
    {
        _resourceManager.Zones.CollectionChanged -= Zones_CollectionChanged;
        base.Dispose();
    }

}
