using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames.Testing;

// No game logic; driven by hand with !minigame to check the shared lifecycle.
public sealed class TestMiniGameSession : MiniGameSession
{
    private readonly List<string> _events = [];

    public TestMiniGameSession(MiniGameUiDefinition definition)
        : base(definition, WorldMiniGameHost.Instance)
    {
    }

    public IReadOnlyList<string> Events => _events;

    protected override void OnEntered(Player player, IZone zone) => _events.Add($"Entered {player.Name.FullName}");

    protected override void OnStarted() => _events.Add("Started");

    protected override void OnPlayerStarted(Player player) => _events.Add($"PlayerStarted {player.Name.FullName}");

    protected override void OnLeft(Player player, MiniGameEndReason reason) => _events.Add($"Left {player.Name.FullName} ({reason})");

    protected override void OnFinished(MiniGameResult result) => _events.Add($"Finished ({result.EarnedStars} stars)");

    protected override void OnClosed() => _events.Add("Closed");
}
