using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class MiniGameNpcHelper
{
    public static bool TryGetInteractionListPacket(Npc npc, Player player, IResourceManager resourceManager, out CommandPacketInteractionList? commandPacketInteractionList)
    {
        commandPacketInteractionList = null;

        if (!resourceManager.MiniGameNpcs.TryGetValue(npc.NameId, out var miniGameNpcDefinition))
            return false;

        if (!InteractionMenuHelper.CanInteract(npc, player))
            return true;

        var interactions = miniGameNpcDefinition.Buttons
            .Where(miniGameNpcButtonDefinition => TryGetActivity(resourceManager, miniGameNpcButtonDefinition, out _))
            .Select(miniGameNpcButtonDefinition => new InteractionData
            {
                Id = miniGameNpcButtonDefinition.Id,
                IconId = miniGameNpcButtonDefinition.IconId,
                ButtonText = miniGameNpcButtonDefinition.ButtonText,
                Type = (int)miniGameNpcButtonDefinition.Type,
                Param1 = miniGameNpcButtonDefinition.Param1
            });

        commandPacketInteractionList = InteractionMenuHelper.GetInteractionListPacket(npc.Guid, string.Empty, interactions, autoSelectSingle: true);

        return true;
    }

    public static bool TryInteract(Player player, Npc npc, int buttonId, IResourceManager resourceManager)
    {
        if (!resourceManager.MiniGameNpcs.TryGetValue(npc.NameId, out var miniGameNpcDefinition))
            return false;

        var miniGameNpcButtonDefinition = miniGameNpcDefinition.Buttons.FirstOrDefault(button => button.Id == buttonId);

        if (miniGameNpcButtonDefinition is null)
            return false;

        if (!InteractionMenuHelper.CanInteract(npc, player) ||
            !TryGetActivity(resourceManager, miniGameNpcButtonDefinition, out var clientActivityDefinition))
            return true;

        var definition = ActivityHelper.GetMiniGameUiDefinition(resourceManager, clientActivityDefinition);

        CombatEncounterHelper.TryShowDetails(player, definition, resourceManager);

        return true;
    }

    private static bool TryGetActivity(IResourceManager resourceManager, MiniGameNpcButtonDefinition miniGameNpcButtonDefinition, out ClientActivityDefinition clientActivityDefinition)
    {
        if (miniGameNpcButtonDefinition.Type == MiniGameNpcButtonType.MiniGame)
            return resourceManager.Activities.TryGetByMiniGameId(ActivityHelper.MiniGameAppSystemId, miniGameNpcButtonDefinition.Param1, out clientActivityDefinition!);

        return resourceManager.Activities.TryGet(miniGameNpcButtonDefinition.Param1, out clientActivityDefinition!);
    }
}
