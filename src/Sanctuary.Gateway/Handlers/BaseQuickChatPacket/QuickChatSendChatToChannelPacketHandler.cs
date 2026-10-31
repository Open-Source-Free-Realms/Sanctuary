using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Gateway.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class QuickChatSendChatToChannelPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuickChatSendChatToChannelPacketHandler));
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
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

        if (_resourceManager.QuickChats.TryGetValue(packet.Id, out var quickChatDefinition)
            && OutfitGestureHelper.IsOutfitQuickChat(quickChatDefinition))
        {
            OutfitGestureHelper.Hold(connection, packet, quickChatDefinition.AnimationId);
            return true;
        }

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

            case ChatChannel.WorldArea:
                {
                    packet.AreaNameId = connection.Player.ChatAreaNameId;

                    connection.Player.SendTunneled(packet);

                    if (packet.AreaNameId == 0)
                        break;

                    foreach (var zonePlayer in connection.Player.Zone.Players)
                    {
                        if (zonePlayer.Guid == connection.Player.Guid || zonePlayer.ChatAreaNameId != packet.AreaNameId)
                            continue;

                        if (!zonePlayer.IsChatChannelOn(packet.Channel))
                            continue;

                        if (zonePlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        zonePlayer.SendTunneled(packet);
                    }
                }
                break;

            case ChatChannel.WorldTrade:
            case ChatChannel.WorldLfg:
            case ChatChannel.WorldMembersOnly:
                {
                    connection.Player.SendTunneled(packet);

                    foreach (var zonePlayer in connection.Player.Zone.Players)
                    {
                        if (zonePlayer.Guid == connection.Player.Guid || !zonePlayer.IsChatChannelOn(packet.Channel))
                            continue;

                        if (zonePlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        zonePlayer.SendTunneled(packet);
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