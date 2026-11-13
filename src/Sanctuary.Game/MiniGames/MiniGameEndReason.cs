namespace Sanctuary.Game.MiniGames;

public enum MiniGameEndReason
{
    /// <summary>The game completed and the player saw the end screen.</summary>
    Finished,

    /// <summary>The player left before the game completed.</summary>
    Aborted,

    /// <summary>The game was stopped for everyone without results.</summary>
    Ended,

    Disconnected
}
