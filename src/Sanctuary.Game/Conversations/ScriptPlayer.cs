using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Quests;
using Sanctuary.Scripting;

namespace Sanctuary.Game.Conversations;

/// <summary>
/// Exposes a player to GM-authored scripts.
/// </summary>
public sealed class ScriptPlayer(Player player, IRewardManager rewardManager, IQuestManager questManager) : IScriptablePlayer
{
    public Player Player => player;

    public ulong Guid => player.Guid;
    public string Name => player.Name.FullName;
    public (float x, float y, float z) Position => (player.Position.X, player.Position.Y, player.Position.Z);

    public void Message(string message) => ChatHelper.SendSystemMessage(player, message);

    public void GiveItem(int itemDefinitionId, int quantity) => rewardManager.TryGrantItem(player, itemDefinitionId, 0, quantity);

    public void AcceptQuest(int questId) => questManager.AcceptQuest(player, questId);

    public bool HasCompletedQuest(int questId) => player.Quests.IsCompleted(questId);
}
