using Sanctuary.Game.Entities;
using Sanctuary.Game.MiniGames;
using Sanctuary.Game.MiniGames.CombatEncounters;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class CombatEncounterHelper
{
    private const int CombatJobSwitchedMessageId = 634;
    private const int MiniGameJobSwitchedMessageId = 4594;

    public static bool TryShowDetails(Player player, MiniGameUiDefinition definition, IResourceManager resourceManager)
    {
        if (player.MiniGameUiSession is { } miniGameUiSession &&
            (miniGameUiSession.State != MiniGameUiState.Details || miniGameUiSession.Definition.GameId == definition.GameId))
            return false;

        ClientPcProfile? requiredProfile = null;

        if (resourceManager.Activities.TryGet(definition.GameId, out var clientActivityDefinition) &&
            !ActivityHelper.TryGetRequiredProfile(player, resourceManager, clientActivityDefinition, out requiredProfile))
            return false;

        if (requiredProfile is not null && requiredProfile.Type != player.ActiveProfile.Type)
        {
            player.ActivateProfile(requiredProfile);

            player.SendTunneled(new EncounterParticipantMessagePacket
            {
                MessageId = requiredProfile.Type == (int)ProfileType.Combat ? CombatJobSwitchedMessageId : MiniGameJobSwitchedMessageId
            });
        }

        resourceManager.CombatEncounters.TryGetValue(definition.GameId, out var combatEncounter);

        MiniGameHelper.ShowDetails(player, definition, combatEncounter);

        return true;
    }

    public static void Go(Player player, MiniGameUiSession miniGameUiSession)
    {
        if (miniGameUiSession.State != MiniGameUiState.Details || miniGameUiSession.CombatEncounter is not { } combatEncounter)
            return;

        combatEncounter.OnGo(player, miniGameUiSession);
    }

    public static void Close(Player player, MiniGameUiSession miniGameUiSession, IZoneManager zoneManager)
    {
        if (miniGameUiSession.State == MiniGameUiState.Leaving)
            return;

        if (miniGameUiSession.State == MiniGameUiState.Details || miniGameUiSession.CombatEncounter is not { } combatEncounter)
        {
            MiniGameHelper.Close(player, miniGameUiSession);
            return;
        }

        MiniGameHelper.Leave(player, miniGameUiSession);

        combatEncounter.OnClose(player, miniGameUiSession, zoneManager);
    }
}
