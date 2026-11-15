using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames;

public sealed class EncounterMiniGameClient : IMiniGameClient
{
    private const string UnavailableMessage = "This minigame isn't available yet.";

    public void ShowDetails(MiniGameUiSession participant)
        => MiniGameHelper.ShowDetails(participant);

    public void RefreshDetails(MiniGameUiSession participant)
        => MiniGameHelper.RefreshDetails(participant.Player, participant);

    public void Start(MiniGameUiSession participant)
        => MiniGameHelper.Start(participant.Player, participant);

    public void ShowEndScreen(MiniGameUiSession participant, MiniGameResult result)
        => MiniGameHelper.ShowEndScreen(participant.Player, participant, result.Scores, result.EarnedStars, result.TimeRemaining,
            result.RewardBundle, result.MemberRewardBundle, result.LootWheelRewardBundle);

    public void Leave(MiniGameUiSession participant)
        => MiniGameHelper.Leave(participant.Player, participant);

    public void Close(MiniGameUiSession participant)
        => MiniGameHelper.Close(participant.Player, participant);

    public void Reset(Player player)
        => MiniGameHelper.ResetEncounterData(player);

    public void ProfileSwitched(Player player, ClientPcProfile profile, int messageId)
    {
        player.ActivateProfile(profile);
        player.SendTunneled(new EncounterParticipantMessagePacket { MessageId = messageId });
    }

    public void Unavailable(Player player)
        => ChatHelper.SendSystemMessage(player, UnavailableMessage);
}
