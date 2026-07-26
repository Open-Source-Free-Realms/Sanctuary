using System;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Packet.Common.Chat;
using Sanctuary.Gateway.Helpers;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketChatHandler
{
    private static ILogger _logger = null!;
    private static ILogger _chatLogger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketChatHandler));
        _chatLogger = loggerFactory.CreateLogger("Chat");

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        var adminLogger = loggerFactory.CreateLogger("Admin");

        ChatCommandRegistry.Initialize(_zoneManager, _dbContextFactory, adminLogger);
    }

    private static void SendMuteNotice(GatewayConnection connection)
    {
        DateTimeOffset? mutedUntil = connection.Player.MutedUntil;

        var packet = new PacketChat
        {
            Channel = ChatChannel.System,
            FromName = connection.Player.Name,
            ToName = connection.Player.Name,
            Message = $"You are muted until {mutedUntil:u} and cannot send chat messages."
        };

        connection.Player.SendTunneled(packet);
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketChat.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketChat));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketChat), packet);

        if (packet.Message == null)
        {
            _logger.LogWarning("Received {name} packet with null message. ( {packet} )", nameof(PacketChat), packet);
            return false;
        }

        if (packet.Message.StartsWith("!navto"))
        {
            var args = packet.Message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            NavTo(connection, args);
            return true;
        }

        if (packet.Message.StartsWith("!pos"))
        {
            Position(connection, Array.Empty<string>());
            return true;
        }
        
        if (packet.Message.StartsWith("!admin"))
        {
            ChatCommandRegistry.HandleCommand(connection, packet.Message);
            return true;
        }

        

        if (connection.Player.IsMuted())
        {
            SendMuteNotice(connection);
            return true;
        }

        packet.FromGuid = connection.Player.Guid;
        packet.FromName = connection.Player.Name;

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

                        if (toPlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            break;

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


    private static void NavTo(GatewayConnection connection, string[] args)
    {
        if (args.Length != 4 ||
            !float.TryParse(args[1], out var x) ||
            !float.TryParse(args[2], out var y) ||
            !float.TryParse(args[3], out var z))
        {
            ChatHelper.SendSystemMessage(connection, "Usage: !navto [x] [y] [z]");
            return;
        }

        var player = connection.Player;

        if (!player.Zone.TryCreateNpc(null, out var npc))
        {
            ChatHelper.SendSystemMessage(connection, "Failed to spawn a test NPC.");
            return;
        }

        npc.NameId = 437129;
        npc.ModelId = 3927;
        npc.Scale = 1f;
        npc.Disposition = 0;
        npc.HideNamePlate = false;
        npc.MovementType = 2;

        npc.Visible = true;
        npc.UpdatePosition(player.Position, player.Rotation);

        npc.MoveTo(new Vector3(x, y, z));

        _logger.LogInformation("NavTo test spawn requested by {Player} to ({X}, {Y}, {Z})", player.Name, x, y, z);
        ChatHelper.SendSystemMessage(connection, $"Spawned NPC {npc.Guid} moving to ({x:0.0}, {y:0.0}, {z:0.0}).");
    }

    private static void Position(GatewayConnection connection, string[] args)
    {
        var player = connection.Player;
        var position = player.Position;

        _logger.LogInformation(
            "POSITION | Player: {Name} | Zone: {Zone} | Position: ({X:F3}, {Y:F3}, {Z:F3})",
            player.Name, player.Zone.Name, position.X, position.Y, position.Z);

        ChatHelper.SendSystemMessage(connection, $"Position: ({position.X:F2}, {position.Y:F2}, {position.Z:F2})");
    }
}