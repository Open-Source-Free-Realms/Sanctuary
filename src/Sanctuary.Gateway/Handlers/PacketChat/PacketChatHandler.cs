using System;
using System.Linq;
using System.Numerics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketChatHandler
{
    private static ILogger _logger = null!;
    private static ILogger _chatLogger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketChatHandler));
        _chatLogger = loggerFactory.CreateLogger("Chat");

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketChat.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketChat));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketChat), packet);

        packet.FromGuid = connection.Player.Guid;
        packet.FromName = connection.Player.Name;

        // Debug commands (not broadcast to other players).
        if (TryHandleCommand(connection, packet.Message))
            return true;

        switch (packet.Channel)
        {
            case ChatChannel.Tell:
                {
                    if (_zoneManager.TryGetPlayer(packet.ToName.FullName, out var toPlayer))
                    {
                        _chatLogger.LogInformation("Tell|From: \"{FromName}\" ({FromGuid}), To: \"{ToName}\" ({ToGuid}), Msg: \"{Message}\"",
                            packet.FromName,
                            packet.FromGuid,
                            packet.ToName,
                            toPlayer.Guid,
                            packet.Message
                        );

                        if (!toPlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            toPlayer.SendTunneled(packet);

                        var tellEchoPacket = new TellEchoPacket();

                        tellEchoPacket.Name = packet.ToName;
                        tellEchoPacket.Message = packet.Message;

                        connection.Player.SendTunneled(tellEchoPacket);
                    }
                }
                break;

            case ChatChannel.WorldShout:
                {
                    _chatLogger.LogInformation("WorldShout|From: \"{FromName}\" ({FromGuid}), Msg: \"{Message}\"",
                        packet.FromName,
                        packet.FromGuid,
                        packet.Message
                    );

                    foreach (var zonePlayer in connection.Player.Zone.Players)
                    {
                        if (zonePlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        zonePlayer.SendTunneled(packet);
                    }
                }
                break;

            case ChatChannel.WorldTrade:
            case ChatChannel.WorldLfg:
            case ChatChannel.WorldArea:
            case ChatChannel.WorldMembersOnly:
                {
                    _chatLogger.LogInformation("{Channel}|Area: {AreaNameId}, From: \"{FromName}\" ({FromGuid}), Msg: \"{Message}\"",
                        packet.Channel,
                        packet.AreaNameId,
                        packet.FromName,
                        packet.FromGuid,
                        packet.Message
                    );

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
                    _chatLogger.LogInformation("{Channel}|From: \"{FromName}\" ({FromGuid}), Msg: \"{Message}\"",
                        packet.Channel,
                        packet.FromName,
                        packet.FromGuid,
                        packet.Message
                    );

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

    /// <summary>
    /// Handles debug commands typed in chat. Returns true if the message was a command (and therefore
    /// should not be broadcast as normal chat). The client swallows most unknown "/slash" commands and
    /// never sends them, so these also match with NO leading slash (e.g. just type "tporigin").
    /// </summary>
    private static bool TryHandleCommand(GatewayConnection connection, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var text = message.Trim().TrimStart('/');

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            // Teleport to a world position (default: the world origin 0,0,0). Debug tool for checking
            // where stray/preloaded actors end up. Usage: tporigin  or  tp <x> <y> <z>
            case "tporigin":
            case "tp":
            {
                float x = 0f, y = 0f, z = 0f;
                if (parts.Length >= 4)
                {
                    float.TryParse(parts[1], out x);
                    float.TryParse(parts[2], out y);
                    float.TryParse(parts[3], out z);
                }

                var pos = new Vector4(x, y, z, 1f);

                // Same teleport path the real teleport handler uses.
                connection.SendTunneled(new ClientUpdatePacketUpdateLocation
                {
                    Position = pos,
                    Rotation = connection.Player.Rotation,
                    Teleport = true
                });

                _logger.LogInformation("Player {guid} teleported via '{command}' -> {pos}", connection.Player.Guid, command, pos);
                return true;
            }

            default:
                return false;
        }
    }
}