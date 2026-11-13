using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames;

public interface IMiniGameClient
{
    void ShowDetails(MiniGameUiSession participant);
    void RefreshDetails(MiniGameUiSession participant);
    void Start(MiniGameUiSession participant);
    void ShowEndScreen(MiniGameUiSession participant, MiniGameResult result);

    /// <summary>Terminates the encounter while the player is still loading back out.</summary>
    void Leave(MiniGameUiSession participant);

    /// <summary>Terminates the encounter and restores the default encounter data.</summary>
    void Close(MiniGameUiSession participant);

    /// <summary>Restores the default encounter data once the player is back.</summary>
    void Reset(Player player);

    void ProfileSwitched(Player player, ClientPcProfile profile, int messageId);
    void Unavailable(Player player);
}
