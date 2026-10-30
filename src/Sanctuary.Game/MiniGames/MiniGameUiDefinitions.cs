using System.Diagnostics.CodeAnalysis;

namespace Sanctuary.Game.MiniGames;

public static class MiniGameUiDefinitions
{
    public static bool TryGet(IResourceManager resourceManager, int gameId, [MaybeNullWhen(false)] out MiniGameUiDefinition definition)
    {
        if (resourceManager.CombatEncounters.TryGetValue(gameId, out var combatEncounter))
        {
            definition = combatEncounter.Definition;
            return true;
        }

        return resourceManager.SignUpMiniGames.TryGetValue(gameId, out definition);
    }
}
