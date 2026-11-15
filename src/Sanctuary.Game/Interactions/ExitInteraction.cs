using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class ExitInteraction : IInteraction
{
    public const int Type = 34;

    public static readonly InteractionData Data = new() { Id = IInteraction.UniqueId++, Type = Type };

    public int Id => Data.Id;

    private readonly IZoneManager _zoneManager;

    public ExitInteraction(IZoneManager zoneManager)
    {
        _zoneManager = zoneManager;
    }

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Npc npc ||
            npc.InteractionList?.Interactions.Any(x => x.Type == Type) != true ||
            !InteractionMenuHelper.CanInteract(npc, player))
            return;

        HousingHelper.TryLeaveHouse(player, _zoneManager);
    }
}
