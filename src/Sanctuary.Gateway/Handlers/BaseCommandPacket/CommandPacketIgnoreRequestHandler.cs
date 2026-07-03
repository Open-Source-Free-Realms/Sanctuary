using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketIgnoreRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketIgnoreRequestHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketIgnoreRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketIgnoreRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketIgnoreRequest), packet);


        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacterToIgnore = dbContext.Characters.FirstOrDefault(x => x.FullName == packet.Name.FullName);

        if (dbCharacterToIgnore is null)
            return true;

        if (!_zoneManager.TryGetPlayer(GuidHelper.GetPlayerGuid(dbCharacterToIgnore.Id), out var playerToIgnore))
            return true;

        if (packet.Ignore)
        {
            if (connection.Player.Ignores.Any(x => x.Guid == playerToIgnore.Guid))
                return true;

            var dbCharacter = dbContext.Characters.FirstOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

            if (dbCharacter is null)
                return true;

            dbCharacter.Ignores.Add(new DbIgnore
            {
                CharacterId = dbCharacter.Id,
                IgnoreCharacterId = dbCharacterToIgnore.Id,
            });

            if (dbContext.SaveChanges() <= 0)
                return true;

            var ignoreData = new IgnoreData
            {
                Guid = playerToIgnore.Guid,
                Name = playerToIgnore.Name.FullName
            };

            connection.Player.Ignores.Add(ignoreData);

            var ignoreAddPacket = new IgnoreAddPacket
            {
                Ignore = ignoreData
            };

            connection.SendTunneled(ignoreAddPacket);
        }
        else
        {
            var dbIgnoreToRemove = dbContext.Ignores.Where(x =>
                x.CharacterId == GuidHelper.GetPlayerId(connection.Player.Guid) &&
                x.IgnoreCharacterId == dbCharacterToIgnore.Id);

            if (dbIgnoreToRemove.ExecuteDelete() <= 0)
                return true;

            connection.Player.Ignores.RemoveAll(x => x.Guid == playerToIgnore.Guid);

            var ignoreRemovePacket = new IgnoreRemovePacket
            {
                Guid = playerToIgnore.Guid
            };

            connection.SendTunneled(ignoreRemovePacket);
        }

        return true;
    }
}