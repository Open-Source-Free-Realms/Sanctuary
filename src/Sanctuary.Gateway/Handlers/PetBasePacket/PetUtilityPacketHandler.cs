using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PetUtilityPacketHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PetUtilityPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PetUtilityPacket.TryDeserialize(data, out var petUtilityPacket))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PetUtilityPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PetUtilityPacket), petUtilityPacket);

        if (petUtilityPacket.Slot < PetHelper.PetFirstUtilitySlot || petUtilityPacket.Slot > PetHelper.PetLastUtilitySlot)
            return true;

        var petClientInfo = connection.Player.GetPetClientInfo(petUtilityPacket.PetId);

        if (petClientInfo is null)
            return true;

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!PetHelper.TryGetDbPet(dbContext, characterId, petUtilityPacket.PetId, out var dbPet)
            || (!petUtilityPacket.Unequip && !PetHelper.TryGetUtilityItem(dbContext, _resourceManager, characterId, petClientInfo,
                petUtilityPacket.ItemGuid, petUtilityPacket.Slot, out _, out _)))
        {
            return true;
        }

        var itemGuid = petUtilityPacket.Unequip ? PetHelper.NoItemGuid : petUtilityPacket.ItemGuid;
        PetHelper.SetUtility(dbPet, petClientInfo, petUtilityPacket.Slot, itemGuid);
        dbContext.SaveChanges();
        connection.SendTunneled(new PetUtilityPacket
        {
            Unequip = petUtilityPacket.Unequip,
            PetId = petUtilityPacket.PetId,
            Slot = petUtilityPacket.Slot,
            ItemGuid = itemGuid
        });

        return true;
    }
}
