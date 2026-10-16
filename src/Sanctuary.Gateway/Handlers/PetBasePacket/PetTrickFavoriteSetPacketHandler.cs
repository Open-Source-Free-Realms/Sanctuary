using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PetTrickFavoriteSetPacketHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    private const int NoTrickId = 0;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PetTrickFavoriteSetPacketHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PetTrickFavoriteSetPacket.TryDeserialize(data, out var petTrickFavoriteSetPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PetTrickFavoriteSetPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PetTrickFavoriteSetPacket), petTrickFavoriteSetPacket);

        if (petTrickFavoriteSetPacket.Slot < PetHelper.PetFirstFavoriteTrickSlot || petTrickFavoriteSetPacket.Slot > PetHelper.PetLastFavoriteTrickSlot)
            return true;

        var petClientInfo = connection.Player.GetPetClientInfo(petTrickFavoriteSetPacket.PetId);

        if (petClientInfo is null || (petTrickFavoriteSetPacket.TrickId != NoTrickId &&
            !petClientInfo.PetTricks.Any(petTrickInfo => petTrickInfo.Id == petTrickFavoriteSetPacket.TrickId && petTrickInfo.Known)))
        {
            return true;
        }

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!PetHelper.TryGetDbPet(dbContext, characterId, petTrickFavoriteSetPacket.PetId, out var dbPet))
            return true;

        PetHelper.SetFavoriteTrick(dbPet, petClientInfo, petTrickFavoriteSetPacket.Slot, petTrickFavoriteSetPacket.TrickId);
        dbContext.SaveChanges();
        connection.SendTunneled(petTrickFavoriteSetPacket);

        return true;
    }
}
