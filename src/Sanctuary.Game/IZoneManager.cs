using System.Diagnostics.CodeAnalysis;

using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public interface IZoneManager
{
    StartingZone StartingZone { get; }

    bool Load();

    bool TryGetOrCreateCombatInstance([MaybeNullWhen(false)] out CombatInstanceZone zone);
    bool IsCombatInstance(IZone zone);

    IEnumerable<Player> GetPlayers();

    bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player);
    bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player);
}
