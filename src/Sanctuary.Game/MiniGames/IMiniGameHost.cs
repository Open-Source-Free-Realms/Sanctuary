using System.Diagnostics.CodeAnalysis;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames;

public interface IMiniGameHost
{
    /// <summary>True when entering loads a zone, so the game starts once the client is ready.</summary>
    bool LoadsZone { get; }

    bool TryEnter(Player player, [MaybeNullWhen(false)] out IZone zone);

    void Leave(Player player);
}
