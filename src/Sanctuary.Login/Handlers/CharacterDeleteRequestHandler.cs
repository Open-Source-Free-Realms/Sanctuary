using System;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Database;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class CharacterDeleteRequestHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CharacterDeleteRequestHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!CharacterDeleteRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CharacterDeleteRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CharacterDeleteRequest), packet);

        var characterDeleteReply = new CharacterDeleteReply();

        if (connection.Guid == 0)
        {
            characterDeleteReply.Status = 2;

            connection.Send(characterDeleteReply);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var character = dbContext.Characters.SingleOrDefault(x => x.UserGuid == connection.Guid && x.Guid == packet.EntityKey);

        if (character is null)
        {
            characterDeleteReply.Status = 2;

            connection.Send(characterDeleteReply);

            return true;
        }

        dbContext.Remove(character);

        if (dbContext.SaveChanges() <= 0)
        {
            characterDeleteReply.Status = 2;

            connection.Send(characterDeleteReply);
        }

        characterDeleteReply.Status = 1;
        characterDeleteReply.EntityKey = packet.EntityKey;

        connection.Send(characterDeleteReply);

        return true;
    }
}