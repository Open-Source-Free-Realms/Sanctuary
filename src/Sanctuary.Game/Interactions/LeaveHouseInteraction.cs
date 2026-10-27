using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

/// <summary>
/// The exit gate/door of a house.
/// </summary>
public sealed class LeaveHouseInteraction : IInteraction
{
    public static readonly InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 1345,
        ButtonText = 4826 // Exit
    };

    private readonly IHousingManager _housingManager;

    public int Id => Data.Id;

    public LeaveHouseInteraction(IHousingManager housingManager)
    {
        _housingManager = housingManager;
    }

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Npc exit || !InteractionMenuHelper.CanInteract(exit, player))
            return;

        _housingManager.TryLeaveHouse(player);
    }
}
