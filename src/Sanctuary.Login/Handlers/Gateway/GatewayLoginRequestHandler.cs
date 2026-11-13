using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class GatewayLoginRequestHandler
{
    private static ILogger _logger = null!;
    private static LoginServerOptions _options = null!;
    private static GatewayServer _gatewayServer = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GatewayLoginRequestHandler));

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<LoginServerOptions>>();
        _options = options.CurrentValue;
        options.OnChange(o => _options = o);

        _gatewayServer = serviceProvider.GetRequiredService<GatewayServer>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!GatewayLoginRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GatewayLoginRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GatewayLoginRequest), packet);

        var gatewayLoginReply = new GatewayLoginReply();

        if (_options.LoginGatewayChallenge != packet.Challenge)
        {
            connection.Send(gatewayLoginReply);
            connection.Disconnect();

            _logger.LogWarning("Gateway tried to connect but failed the challenge, \"{challenge}\".", packet.Challenge);

            return true;
        }

        if (string.IsNullOrEmpty(packet.ServerAddress))
        {
            connection.Send(gatewayLoginReply);
            connection.Disconnect();

            _logger.LogWarning("Gateway tried to connect but is missing the server address, \"{serveraddress}\".", packet.ServerAddress);

            return true;
        }

        connection.ServerData = packet.Data;
        connection.ServerAddress = packet.ServerAddress;

        // A gateway that logs in with an address already registered is that gateway restarted. Its old connection
        // can linger until the UDP timeout; drop it now, or character logins keep going to the dead gateway.
        foreach (var previous in _gatewayServer.Gateways.Where(x => x != connection && x.ServerAddress == packet.ServerAddress).ToList())
        {
            _logger.LogInformation("Gateway {address} reconnected; dropping its previous connection {previous}.", packet.ServerAddress, previous);
            previous.Disconnect();
        }

        gatewayLoginReply.Success = true;

        connection.Send(gatewayLoginReply);

        return true;
    }
}