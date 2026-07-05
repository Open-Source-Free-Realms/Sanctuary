using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketRemoveFriendRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketRemoveFriendRequestHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketRemoveFriendRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketRemoveFriendRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketRemoveFriendRequest), packet);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var requesterCharacterId = GuidHelper.GetPlayerId(connection.Player.Guid);

        var dbCharacterToRemove = dbContext.Characters.FirstOrDefault(x => x.FullName == packet.Name.FullName);
        if (dbCharacterToRemove is null)
        {
            SendSystemMessage(connection, "Player not found.");
            return true;
        }

        var targetCharacterId = dbCharacterToRemove.Id;
        var targetGuid = GuidHelper.GetPlayerGuid(targetCharacterId);

        // Delete both directions from the Friends table.
        var removed = dbContext.Friends
            .Where(x =>
                (x.CharacterId == requesterCharacterId && x.FriendCharacterId == targetCharacterId) ||
                (x.CharacterId == targetCharacterId && x.FriendCharacterId == requesterCharacterId))
            .ExecuteDelete();

        if (removed <= 0)
        {
            SendSystemMessage(connection, "That player is not on your friends list.");
            return true;
        }

        // Update the requester's in-memory friends list.
        connection.Player.Friends.RemoveAll(x => x.Guid == targetGuid);

        // If the other player is online, update their in-memory list too.
        if (_zoneManager.TryGetPlayer(targetGuid, out var targetPlayer))
            targetPlayer.Friends.RemoveAll(x => x.Guid == connection.Player.Guid);

        // Remove from the client UI immediately.
        connection.SendTunneled(new FriendRemovePacket
        {
            Guid = targetGuid
        });

        // If the other player is online, remove requester from their UI too.
        if (_zoneManager.TryGetPlayer(targetGuid, out targetPlayer))
        {
            targetPlayer.SendTunneled(new FriendRemovePacket
            {
                Guid = connection.Player.Guid
            });
        }

        return true;
    }

    private static void SendSystemMessage(GatewayConnection connection, string message)
    {
        connection.Player.SendTunneled(new PacketChat
        {
            Channel = ChatChannel.System,
            Message = message
        });
    }
}