using System;
using System.Linq;

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
    /// Returns false, and drops nothing, while any of them is still live.
    /// </summary>
    public bool TryReleaseIdleCharacter(ulong characterId, uint idleThresholdMilliseconds)
    {
        var sessions = ConnectionList
            .Where(x => x.Player is not null && GuidHelper.GetPlayerId(x.Player.Guid) == characterId)
            .ToList();

        foreach (var session in sessions)
        {
            var idle = session.MillisecondsSinceLastReceive();

            if (idle < idleThresholdMilliseconds)
            {
                _logger.LogInformation("Character {id} is still live on {connection} (last packet {idle} ms ago).", characterId, session, idle);
                return false;
            }
        }

        foreach (var session in sessions)
        {
            _logger.LogInformation("Dropping silent {connection} so character {id} can log in again.", session, characterId);
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
