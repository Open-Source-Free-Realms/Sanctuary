using System.Diagnostics.CodeAnalysis;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames;

public sealed class WorldMiniGameHost : IMiniGameHost
{
    public static WorldMiniGameHost Instance { get; } = new();

    public bool LoadsZone => false;

    public bool TryEnter(Player player, [MaybeNullWhen(false)] out IZone zone)
    {
        zone = player.Zone;
        return true;
    }

    public void Leave(Player player)
    {
    }
}
