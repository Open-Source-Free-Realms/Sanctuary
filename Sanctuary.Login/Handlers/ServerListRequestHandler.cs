using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Core.Configuration;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class ServerListRequestHandler
{
    private static ILogger _logger = null!;
    private static LoginServerOptions _options = null!;
    private static GatewayServer _gatewayServer = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ServerListRequestHandler));

        _gatewayServer = serviceProvider.GetRequiredService<GatewayServer>();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<LoginServerOptions>>();
        _options = options.CurrentValue;
        options.OnChange(o => _options = o);
    }

    public static bool HandlePacket(LoginConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(ServerListRequest));

        var serverListReply = new ServerListReply();

        foreach (var gateways in _gatewayServer.Gateways)
        {
            var clientGameServerData = new ClientGameServerData
            {
                Data = gateways.ServerData
            };

            serverListReply.Servers.Add(clientGameServerData);
        }

        connection.Send(serverListReply);

        return true;
    }
}