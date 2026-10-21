using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Sanctuary.Game.MiniGames.CombatEncounters;

namespace Sanctuary.Game.MiniGames;

public static class MiniGameUiDefinitions
{
    private static readonly Dictionary<int, MiniGameUiDefinition> _definitions = CombatEncounterRegistry.Encounters
        .Select(combatEncounter => combatEncounter.Definition)
        .Concat(SignUpMiniGameDefinitions.Definitions)
        .ToDictionary(definition => definition.GameId);

    public static bool TryGet(int gameId, [MaybeNullWhen(false)] out MiniGameUiDefinition definition)
    {
        return _definitions.TryGetValue(gameId, out definition);
    }
}
