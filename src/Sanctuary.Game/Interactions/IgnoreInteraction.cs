using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class IgnoreInteraction : IInteraction
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 9557,
        ButtonText = 2816
    };

    public IgnoreInteraction(IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Player otherPlayer)
            return;

        if (player.Ignores.Any(x => x.Guid == otherPlayer.Guid))
            return;

        if (player.Friends.Any(x => x.Guid == otherPlayer.Guid))
            return;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.FirstOrDefault(x => x.Id == GuidHelper.GetPlayerId(player.Guid));

        if (dbCharacter is null)
            return;

        dbCharacter.Ignores.Add(new DbIgnore
        {
            CharacterId = dbCharacter.Id,
            IgnoreCharacterId = GuidHelper.GetPlayerId(otherPlayer.Guid),
        });

        if (dbContext.SaveChanges() <= 0)
            return;

        var ignoreData = new IgnoreData
        {
            Guid = otherPlayer.Guid,
            Name = otherPlayer.Name.FullName
        };

        player.Ignores.Add(ignoreData);

        var ignoreAddPacket = new IgnoreAddPacket
        {
            Ignore = ignoreData
        };

        player.SendTunneled(ignoreAddPacket);
    }
}