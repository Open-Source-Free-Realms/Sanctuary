using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public interface IZoneManager
{
    int StartingZoneDefinitionId { get; }

    WorldZone StartingZone { get; }

    IEnumerable<IZone> Zones { get; }

    bool Load();

    bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player);
    bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player);

    bool TryMovePlayerToZone(int zoneDefinitionId, ulong? ownerId, Player player, out IZone zone);

    void RemoveZoneInstance(IZone zone);
    void EvictIfEmpty(IZone zone);
}