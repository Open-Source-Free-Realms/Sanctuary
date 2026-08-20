using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public interface IZoneManager
{
    WorldZone StartingZone { get; }

    IEnumerable<IZone> Zones { get; }

    bool Load();

    bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player);
    bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player);

    bool TryGetOrCreateZoneInstance(int zoneDefinitionId, ulong? ownerId, [MaybeNullWhen(false)] out IZone zone);
    bool TryGrantHouse(int zoneDefinitionId, ulong ownerId);

    void RemoveZoneInstance(IZone zone);
}