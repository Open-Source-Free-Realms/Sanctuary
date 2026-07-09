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

        Handle(connection, packet.Name);

        return true;
    }

    public static void Handle(GatewayConnection connection, NameData name)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacterToRemove = dbContext.Characters
            .AsNoTracking()
            .Include(x => x.Friends)
                .ThenInclude(x => x.FriendCharacter)
            .FirstOrDefault(x => x.FullName == name.FullName);

        if (dbCharacterToRemove is null)
            return;

        var dbFriendsToRemove = dbContext.Friends.Where(x => (x.CharacterId == dbCharacterToRemove.Id &&
                                                              x.FriendCharacterId == GuidHelper.GetPlayerId(connection.Player.Guid)) ||
                                                             (x.FriendCharacterId == dbCharacterToRemove.Id &&
                                                             x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid)));

        if (dbFriendsToRemove.ExecuteDelete() <= 0)
            return;

        var dbCharacterToRemoveGuid = GuidHelper.GetPlayerGuid(dbCharacterToRemove.Id);

        connection.Player.Friends.RemoveAll(x => x.Guid == dbCharacterToRemoveGuid);

        connection.Player.SendTunneled(new FriendRemovePacket
        {
            Guid = dbCharacterToRemoveGuid
        });

        if (_zoneManager.TryGetPlayer(dbCharacterToRemoveGuid, out var player))
        {
            player.Friends.RemoveAll(x => x.Guid == connection.Player.Guid);

            player.SendTunneled(new FriendRemovePacket
            {
                Guid = connection.Player.Guid
            });
        }
    }
}