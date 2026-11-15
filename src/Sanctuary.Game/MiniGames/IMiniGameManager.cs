using Sanctuary.Game.Entities;

namespace Sanctuary.Game.MiniGames;

public interface IMiniGameManager
{
    bool ShowDetails(Player player, int activityId);

    bool Open(Player player, MiniGameSession session);

    void RefreshDetails(Player player);

    bool Enter(Player player, int sessionId);

    void ClientIsReady(Player player);

    /// <summary>The player cancelled the details screen. Ignored once the game has been entered.</summary>
    bool CancelDetails(Player player);

    /// <summary>The player closed the end screen. Ignored on the details screen.</summary>
    bool CloseEndScreen(Player player);

    /// <summary>The player left from any state.</summary>
    bool Leave(Player player);

    bool Finish(Player player, MiniGameResult result);

    bool End(Player player);

    void Disconnect(Player player);
}
