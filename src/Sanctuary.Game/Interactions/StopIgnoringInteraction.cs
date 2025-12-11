using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class StopIgnoringInteraction : IInteraction
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 12052,
        ButtonText = 2817
    };

    public StopIgnoringInteraction(IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Player otherPlayer)
            return;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbIgnoreToRemove = dbContext.Ignores.Where(x =>
                x.CharacterId == GuidHelper.GetPlayerId(player.Guid) &&
                x.IgnoreCharacterId == GuidHelper.GetPlayerId(otherPlayer.Guid));

        if (dbIgnoreToRemove.ExecuteDelete() <= 0)
            return;

        player.Ignores.RemoveAll(x => x.Guid == otherPlayer.Guid);

        var ignoreRemovePacket = new IgnoreRemovePacket
        {
            Guid = otherPlayer.Guid
        };

        player.SendTunneled(ignoreRemovePacket);
    }
}