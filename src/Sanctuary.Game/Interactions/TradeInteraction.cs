using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class TradeInteraction : IInteraction
{
    private readonly TradeManager _trades;

    public TradeInteraction(TradeManager trades) => _trades = trades;

    public int Id => Data.Id;

    public static readonly InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 41385,
        ButtonText = 3299,
        Type = 0
    };

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Player target || target.Guid == player.Guid)
            return;

        if (System.Linq.Enumerable.Any(target.Ignores, ignored => ignored.Guid == player.Guid))
            return;

        _trades.Invite(player, target);
    }
}
