using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public sealed class CombatEncounterSession : MiniGameSession
{
    private readonly CombatEncounter _combatEncounter;

    public CombatEncounterSession(MiniGameUiDefinition definition, CombatEncounter combatEncounter, IMiniGameHost host)
        : base(definition, host)
    {
        _combatEncounter = combatEncounter;
    }

    protected override void OnEntered(Player player, IZone zone)
    {
        if (!zone.Npcs.Any())
            _combatEncounter.SpawnNpcs(zone);
    }
}
