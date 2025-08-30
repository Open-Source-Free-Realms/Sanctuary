using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class InspectInteraction : IInteraction
{
    private readonly IResourceManager _resourceManager;

    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 133,
        ButtonText = 3902
    };

    public InspectInteraction(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Player otherPlayer)
            return;

        var startInspectPacket = new StartInspectPacket()
        {
            Guid = otherPlayer.Guid,
            ShowPedestal = true
        };

        var inspectProxy = new InspectProxy();

        foreach (var profile in otherPlayer.Profiles)
        {
            inspectProxy.Profiles.Add(new InspectProxy.ProfileEntry
            {
                Id = profile.Id,
                Rank = profile.Rank
            });
        }

        inspectProxy.ActiveProfileId = otherPlayer.ActiveProfileId;

        foreach (var profileItem in otherPlayer.ActiveProfile.Items.Values)
        {
            var clientItem = otherPlayer.Items.FirstOrDefault(x => x.Id == profileItem.Id);

            if (clientItem is null)
                continue;

            if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
                continue;

            inspectProxy.Items.Add(new InspectProxy.ItemEntry
            {
                Slot = profileItem.Slot,

                ItemRecord =
                {
                    Definition = clientItem.Definition,
                    Tint = clientItem.Tint
                },
                ItemDefinition = clientItemDefinition
            });
        }

        inspectProxy.VipRank = otherPlayer.VipRank;
        inspectProxy.VipIconId = otherPlayer.VipIconId;
        inspectProxy.VipTitle = otherPlayer.VipTitle;

        inspectProxy.ActiveTitle = otherPlayer.ActiveTitle;

        inspectProxy.Coins = otherPlayer.Coins;

        inspectProxy.LevelsGained = otherPlayer.Profiles.Sum(x => x.Rank - 1);

        foreach (var stat in otherPlayer.Stats)
            inspectProxy.Stats.Add(stat.Value);

        startInspectPacket.Payload = inspectProxy.Serialize();

        player.SendTunneled(startInspectPacket);
    }
}