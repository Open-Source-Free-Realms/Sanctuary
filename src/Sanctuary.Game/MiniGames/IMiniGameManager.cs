using Sanctuary.Game.Entities;

namespace Sanctuary.Game.MiniGames;

public interface IMiniGameManager
{
    bool ShowDetails(Player player, int activityId);

    bool Open(Player player, MiniGameSession session);

    void RefreshDetails(Player player);

    bool Enter(Player player, int sessionId);

    void ClientIsReady(Player player);

    /// <summary>The player left: cancelled the details screen, exited, or closed the end screen.</summary>
    bool Leave(Player player);

    bool Finish(Player player, MiniGameResult result);

    bool End(Player player);

    void Disconnect(Player player);
}
