using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class QuickChatSendChatToChannelPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuickChatSendChatToChannelPacketHandler));
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!QuickChatSendChatToChannelPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(QuickChatSendChatToChannelPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(QuickChatSendChatToChannelPacket), packet);

        if (connection.Player.IsMuted())
            return true;

        packet.Guid = connection.Player.Guid;
        packet.Name = connection.Player.Name;

        switch (packet.Channel)
        {
            case ChatChannel.GuildSay:
                {
                    if (connection.Player.GuildData is null)
                        break;

                    packet.GuildGuid = connection.Player.GuildData.Guid;

                    foreach (var member in connection.Player.GuildData.Members.Values)
                    {
                        if (!_zoneManager.TryGetPlayer(member.Guid, out var guildPlayer))
                            continue;

                        if (guildPlayer.Guid != connection.Player.Guid && guildPlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        guildPlayer.SendTunneled(packet);
                    }
                }
                break;

            case ChatChannel.WorldTrade:
            case ChatChannel.WorldLfg:
            case ChatChannel.WorldArea:
            case ChatChannel.WorldMembersOnly:
                {
                    connection.Player.SendTunneled(packet);

                    foreach (var visiblePlayer in connection.Player.VisiblePlayers)
                    {
                        if (visiblePlayer.Value.ChatChannelStatus.TryGetValue(packet.Channel, out var channelStatus) && !channelStatus)
                            continue;

                        if (visiblePlayer.Value.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        visiblePlayer.Value.SendTunneled(packet);
                    }
                }
                break;

            default:
                {
                    _logger.LogWarning("Unhandled chat channel {channel}.", packet.Channel);
                    connection.Player.SendTunneled(packet);

                    foreach (var visiblePlayer in connection.Player.VisiblePlayers)
                    {
                        if (visiblePlayer.Value.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        visiblePlayer.Value.SendTunneled(packet);
                    }
                }
                break;
        }

        return true;
    }
}