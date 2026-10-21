using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public static class CombatEncounterRegistry
{
    public static IReadOnlyList<CombatEncounter> Encounters { get; } =
    [
        new FrostfangGrowlerEncounter()
    ];

    private static readonly Dictionary<int, CombatEncounter> _encounters = Encounters
        .ToDictionary(combatEncounter => combatEncounter.Definition.GameId);

    public static bool TryGet(int gameId, [MaybeNullWhen(false)] out CombatEncounter combatEncounter)
    {
        return _encounters.TryGetValue(gameId, out combatEncounter);
    }
}
