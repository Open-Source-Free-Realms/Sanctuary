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

    public bool TryGetCharacterIdle(ulong characterId, out long idleMilliseconds)
    {
        foreach (var connection in ConnectionList)
        {
            if (connection.Player is null || GuidHelper.GetPlayerId(connection.Player.Guid) != characterId)
                continue;

            idleMilliseconds = connection.MillisecondsSinceLastReceive();
            return true;
        }

        idleMilliseconds = 0;
        return false;
    }

    public void DisconnectCharacter(ulong characterId)
    {
        foreach (var connection in ConnectionList.ToList())
        {
            if (connection.Player is null || GuidHelper.GetPlayerId(connection.Player.Guid) != characterId)
                continue;

            _logger.LogInformation("Dropping {connection} so character {id} can log in again.", connection, characterId);
            connection.Disconnect();
        }
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
