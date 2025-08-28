using System.Diagnostics.CodeAnalysis;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game;

public interface IZoneManager
{
    StartingZone StartingZone { get; }

    bool Load();

    bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player);
    bool TryGetPlayer(string name, [MaybeNullWhen(false)] out Player player);
}