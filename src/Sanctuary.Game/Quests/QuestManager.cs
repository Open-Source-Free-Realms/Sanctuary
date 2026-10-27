using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Quests;

public sealed class QuestManager : IQuestManager
{
    public const int AllGoalsDoneGoalId = 0;

    public const int GoalDoneCount = -1;

    private readonly IResourceManager _resourceManager;
    private readonly IRewardManager _rewardManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ILogger<QuestManager> _logger;

    public QuestManager(IResourceManager resourceManager, IRewardManager rewardManager, IDbContextFactory<DatabaseContext> dbContextFactory, ILogger<QuestManager> logger)
    {
        _resourceManager = resourceManager;
        _rewardManager = rewardManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public bool IsQuestNpc(int npcId)
        => _resourceManager.Quests.ByGiver.ContainsKey(npcId) || _resourceManager.Quests.ByTarget.ContainsKey(npcId);

    private IEnumerable<(QuestDefinition Quest, QuestProgress Progress, int GoalIndex, QuestGoal Goal)> ActiveGoals(Player player)
    {
        foreach (var progress in player.Quests.InProgress)
        {
            if (!_resourceManager.Quests.TryGet(progress.QuestId, out var quest))
                continue;

            foreach (var goalIndex in progress.OpenGoals.Keys.Order())
                yield return (quest, progress, goalIndex, quest.Goals[goalIndex]);
        }
    }

    public List<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc)
    {
        var options = new List<NpcInteractionOption>();
        var quests = _resourceManager.Quests;

        foreach (var progress in player.Quests.InProgress)
        {
            if (!quests.TryGet(progress.QuestId, out var activeQuest))
                continue;

            if (!AdvancesHere(activeQuest, progress, npc) && !TurnsInHere(activeQuest, progress, npc))
                continue;

            var quest = activeQuest;

            options.Add(new NpcInteractionOption
            {
                IconId = ContextIcons.QuestTurnIn,
                ButtonTextId = quest.TitleId,
                Invoke = interactingPlayer => AdvanceAtNpc(interactingPlayer, quest, npc)
            });
        }

        if (quests.ByGiver.TryGetValue(npc.DefinitionId, out var giverQuestIds))
        {
            foreach (var questId in giverQuestIds)
            {
                if (!quests.TryGet(questId, out var offerableQuest) || !offerableQuest.IsOfferableFor(player.Quests))
                    continue;

                var quest = offerableQuest;

                options.Add(new NpcInteractionOption
                {
                    IconId = ContextIcons.QuestOffer,
                    ButtonTextId = quest.TitleId,
                    Invoke = interactingPlayer => Offer(interactingPlayer, quest)
                });
            }
        }

        return options;
    }

    private static bool AdvancesHere(QuestDefinition quest, QuestProgress progress, Npc npc)
        => TalkGoalHere(quest, progress, npc) >= 0;

    private static int TalkGoalHere(QuestDefinition quest, QuestProgress progress, Npc npc)
    {
        foreach (var goalIndex in progress.OpenGoals.Keys.Order())
        {
            if (quest.Goals[goalIndex].Matches(quest, new NpcTalkedEvent(npc)))
                return goalIndex;
        }

        return -1;
    }

    private static bool TurnsInHere(QuestDefinition quest, QuestProgress progress, Npc npc)
        => progress.GoalIndex >= quest.Goals.Count && quest.GoalTargetNpcId(quest.Goals.Count - 1) == npc.DefinitionId;

    private void AdvanceAtNpc(Player player, QuestDefinition quest, Npc npc)
    {
        if (!player.Quests.TryGetInProgress(quest.QuestId, out var progress))
            return;

        if (TurnsInHere(quest, progress, npc))
        {
            TurnIn(player, quest);
            return;
        }

        var goalIndex = TalkGoalHere(quest, progress, npc);
        if (goalIndex < 0)
            return;

        var goal = quest.Goals[goalIndex];

        if (goal is TalkToNpcGoal { CompleteAfterDialogue: true })
        {
            QuestDialogue.Begin(player, goal.ConversationFor(npc.DefinitionId), npc.Guid, () =>
            {
                if (player.Quests.TryGetInProgress(quest.QuestId, out var current) && current.OpenGoals.ContainsKey(goalIndex))
                    CompleteGoal(player, quest, current, goalIndex, npc, playDialogue: false);
            });
            return;
        }

        var result = goal.Progress(quest, new NpcTalkedEvent(npc), progress.ProgressFor(goalIndex));

        ApplyProgress(player, quest, progress, goalIndex, result, npc);

        if (result != QuestGoalResult.Completed && goal is TalkToNpcGoal { IsCounted: true })
            QuestDialogue.Begin(player, goal.ConversationFor(npc.DefinitionId), npc.Guid);
    }

    public void OnEvent(Player player, QuestEvent questEvent)
    {
        foreach (var (quest, progress, goalIndex, goal) in ActiveGoals(player).ToList())
        {
            if (!progress.OpenGoals.TryGetValue(goalIndex, out var goalProgress))
                continue;

            var result = goal.Progress(quest, questEvent, goalProgress);

            if (result == QuestGoalResult.None)
                continue;

            ApplyProgress(player, quest, progress, goalIndex, result);

            if (questEvent.CreditsOneQuest)
                return;
        }
    }

    private void ApplyProgress(Player player, QuestDefinition quest, QuestProgress progress, int goalIndex, QuestGoalResult result, Npc? npc = null)
    {
        if (result == QuestGoalResult.Completed)
        {
            CompleteGoal(player, quest, progress, goalIndex, npc);
            return;
        }

        if (result == QuestGoalResult.Progressed)
        {
            var goal = quest.Goals[goalIndex];
            var goalProgress = progress.ProgressFor(goalIndex);

            player.SendTunneled(new QuestObjectiveUpdatePacket
            {
                QuestId = quest.QuestId,
                ObjectiveId = goal.NameId,
                CurrentCount = goalProgress.Count,
                CompletedPercentage = QuestCompletion(quest, progress.GoalsDone),
                Unknown6 = RetailObjectiveUpdateUnknown6
            });

            SaveGoalProgress(player, quest.QuestId, goal.NameId, goalProgress);

            if (npc is not null)
                RefreshQuestNotification(player, npc.Guid);
        }

        RefreshObjectiveTarget(player);
    }

    private void SaveQuestRow(Player player, int questId, Action<DbCharacterQuest> update)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var dbQuest = db.CharacterQuests.FirstOrDefault(x => x.QuestId == questId && x.CharacterId == player.CharacterId);
        if (dbQuest is null)
            return;

        update(dbQuest);
        db.SaveChanges();
    }

    private void SaveGoalProgress(Player player, int questId, int goalId, GoalProgress progress)
    {
        using var db = _dbContextFactory.CreateDbContext();

        db.CharacterQuestProgress
            .Where(p => p.CharacterId == player.CharacterId && p.QuestId == questId && p.GoalId == goalId)
            .ExecuteDelete();

        foreach (var (targetId, count) in progress.Save())
        {
            db.CharacterQuestProgress.Add(new DbCharacterQuestProgress
            {
                CharacterId = player.CharacterId,
                QuestId = questId,
                GoalId = goalId,
                TargetId = targetId,
                Count = count
            });
        }

        db.SaveChanges();
    }

    private void SaveGoalDone(Player player, int questId, int goalId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        db.CharacterQuestProgress
            .Where(p => p.CharacterId == player.CharacterId && p.QuestId == questId && p.GoalId == goalId)
            .ExecuteDelete();

        db.CharacterQuestProgress.Add(new DbCharacterQuestProgress
        {
            CharacterId = player.CharacterId,
            QuestId = questId,
            GoalId = goalId,
            TargetId = 0,
            Count = GoalDoneCount
        });

        db.SaveChanges();
    }

    private void DeleteSavedGoalProgress(Player player, int questId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        db.CharacterQuestProgress
            .Where(p => p.CharacterId == player.CharacterId && p.QuestId == questId)
            .ExecuteDelete();
    }

    private void SaveActiveQuestId(Player player, int questId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var dbCharacter = db.Characters.FirstOrDefault(c => c.Id == player.CharacterId);
        if (dbCharacter is null)
            return;

        dbCharacter.ActiveQuestId = questId != 0 ? questId : null;
        db.SaveChanges();
    }

    public void AcceptQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest) || !quest.IsOfferableFor(player.Quests))
            return;

        var progress = new QuestProgress(questId);
        progress.StartStep(quest, 0);

        player.Quests.Add(progress);
        player.Quests.ActiveQuestId = questId;
        player.Quests.LastAcceptedAt = DateTime.UtcNow;

        using (var db = _dbContextFactory.CreateDbContext())
        {
            db.CharacterQuests.Add(new DbCharacterQuest
            {
                QuestId = questId,
                CharacterId = player.CharacterId,
                Completed = false,
                CurrentGoalId = quest.Goals[0].NameId,
                Started = progress.Started
            });

            var dbCharacter = db.Characters.FirstOrDefault(c => c.Id == player.CharacterId);
            if (dbCharacter is not null)
                dbCharacter.ActiveQuestId = questId;

            db.SaveChanges();
        }

        SendActiveState(player, quest);

        RefreshQuestNotifications(player, quest);

        player.SendTunneled(new CommandPacketQuestDialogComplete());
    }

    public bool CompleteQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest))
            return false;

        if (!player.Quests.TryGetInProgress(questId, out var progress) || progress.GoalIndex < quest.Goals.Count)
            return false;

        player.Quests.Complete(questId);
        SaveQuestRow(player, questId, q => q.Completed = true);

        if (player.Quests.ActiveQuestId == questId)
        {
            player.Quests.ActiveQuestId = 0;
            SaveActiveQuestId(player, 0);
        }

        player.SendTunneled(new QuestCompletePacket { QuestId = questId });

        player.SendTunneled(new CompletedQuestCountUpdatePacket
        {
            Count = player.Quests.CompletedCount
        });

        SendJournalQuestStates(player);

        GrantReward(player, quest);

        RefreshQuestNotifications(player, quest);

        if (quest.NextQuestId is { } nextQuestId && _resourceManager.Quests.TryGet(nextQuestId, out var next))
            RefreshQuestNotifications(player, next.GiverNpcId);

        RefreshObjectiveTarget(player);
        return true;
    }

    public bool OfferNextQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest)
            || quest.NextQuestId is not { } nextQuestId
            || !_resourceManager.Quests.TryGet(nextQuestId, out var next)
            || next.GiverNpcId != quest.GoalTargetNpcId(quest.Goals.Count - 1)
            || !next.IsOfferableFor(player.Quests))
            return false;

        Offer(player, next);
        return true;
    }

    private static readonly TimeSpan AbandonBlockedAfterAccept = TimeSpan.FromSeconds(3);

    public void AbandonQuest(Player player, int questId)
    {
        if (DateTime.UtcNow - player.Quests.LastAcceptedAt < AbandonBlockedAfterAccept)
            return;

        if (!player.Quests.TryGetInProgress(questId, out _))
            return;

        if (!_resourceManager.Quests.TryGet(questId, out var quest))
            return;

        RemoveQuest(player, quest);
    }

    public bool ResetQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest) || !player.Quests.Contains(questId))
            return false;

        var wasCompleted = player.Quests.IsCompleted(questId);

        RemoveQuest(player, quest);

        if (wasCompleted)
            player.SendTunneled(new CompletedQuestCountUpdatePacket { Count = player.Quests.CompletedCount });

        return true;
    }

    private void RemoveQuest(Player player, QuestDefinition quest)
    {
        var questId = quest.QuestId;

        player.Quests.Remove(questId);
        QuestDialogue.Clear(player);

        if (player.Quests.PendingTurnInQuestId == questId)
            player.Quests.PendingTurnInQuestId = 0;

        var wasActive = player.Quests.ActiveQuestId == questId;
        if (wasActive)
            player.Quests.ActiveQuestId = 0;

        using (var db = _dbContextFactory.CreateDbContext())
        {
            var dbQuest = db.CharacterQuests.FirstOrDefault(x => x.QuestId == questId && x.CharacterId == player.CharacterId);
            if (dbQuest is not null)
                db.CharacterQuests.Remove(dbQuest);

            if (wasActive)
            {
                var dbCharacter = db.Characters.FirstOrDefault(c => c.Id == player.CharacterId);
                if (dbCharacter is not null)
                    dbCharacter.ActiveQuestId = null;
            }

            db.SaveChanges();
        }

        player.SendTunneled(new QuestAbandonedPacket { QuestId = questId });

        RefreshQuestNotifications(player, quest);

        RefreshObjectiveTarget(player);
    }

    public void SetActiveQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest))
            return;

        if (player.Quests.TryGetInProgress(questId, out var progress))
        {
            player.Quests.ActiveQuestId = questId;
            SaveActiveQuestId(player, questId);

            foreach (var goalIndex in progress.OpenGoals.Keys.Order())
                SendObjectiveActivated(player, questId, quest.Goals[goalIndex]);

            SendObjectiveForGoal(player, quest, progress.FirstOpenGoalIndex);
        }
    }

    public ClientQuestData GetClientQuestData(Player player)
    {
        var questData = new ClientQuestData
        {
            CompletedCount = player.Quests.CompletedCount,
            ActiveQuestId = player.Quests.TryGetInProgress(player.Quests.ActiveQuestId, out _) ? player.Quests.ActiveQuestId : 0
        };

        foreach (var progress in player.Quests.InProgress)
        {
            if (_resourceManager.Quests.TryGet(progress.QuestId, out var quest))
                questData.Quests.Add(ClientQuestFor(player, quest, progress));
        }

        return questData;
    }

    public void SendActiveObjectives(Player player)
    {
        foreach (var (quest, _, _, goal) in ActiveGoals(player))
            SendObjectiveActivated(player, quest.QuestId, goal);
    }

    public void RestoreJournal(Player player)
    {
        RefreshObjectiveTarget(player);

        SendJournalQuestStates(player);
    }

    private void SendJournalQuestStates(Player player)
    {
        var states = new Dictionary<int, int>();
        foreach (var progress in player.Quests.All)
            if (progress.Completed)
                states[progress.QuestId] = 1;

        if (states.Count > 0)
            player.SendTunneled(new AdventurersJournalQuestUpdatePacket { QuestStates = states });
    }

    private void RefreshQuestNotifications(Player player, QuestDefinition quest)
    {
        var npcIds = new HashSet<int> { quest.GiverNpcId, quest.TargetNpcId };

        foreach (var goal in quest.Goals.OfType<TalkToNpcGoal>())
            npcIds.UnionWith(goal.AllTalkTargetNpcIds());

        npcIds.UnionWith(quest.StateNpcIds());

        foreach (var excludedId in quest.ExcludesQuestIds)
        {
            if (!_resourceManager.Quests.TryGet(excludedId, out var excludedQuest))
                continue;

            npcIds.Add(excludedQuest.GiverNpcId);
            npcIds.Add(excludedQuest.TargetNpcId);
        }

        foreach (var npcId in npcIds)
            RefreshQuestNotifications(player, npcId);
    }

    private static void RemoveHiddenNpcs(Player player, QuestGoal goal)
    {
        foreach (var npcId in goal.HideNpcIdsWhenDone)
        {
            foreach (var npc in player.VisibleNpcs.Values.Where(npc => npc.DefinitionId == npcId).ToList())
            {
                player.SendTunneled(EntityHelper.GetRemovePlayerGracefullyPacket(npc.Guid, animate: true, compositeEffectId: goal.HideEffectId));
                player.VisibleNpcs.TryRemove(npc.Guid, out _);
            }
        }
    }

    private void RefreshQuestNotifications(Player player, int npcId)
    {
        if (npcId == 0)
            return;

        foreach (var npc in player.Zone.Npcs.Where(npc => npc.DefinitionId == npcId).ToList())
            RefreshQuestNotification(player, npc.Guid);
    }

    public void RefreshQuestNotification(Player player, ulong npcGuid)
    {
        if (!player.VisibleNpcs.TryGetValue(npcGuid, out var npc))
            return;

        var iconStates = QuestBadgeIconStates(npc);
        if (npc.Notification is not null)
            iconStates.Add(npc.Notification.IconState);

        var removeNotifications = new PlayerUpdatePacketRemoveNotifications();

        foreach (var iconState in iconStates)
            removeNotifications.Notifications.Add(new RemoveNotificationInfo { Guid = npc.Guid, ThoughtBubbleIconId = iconState });

        if (removeNotifications.Notifications.Count > 0)
            player.SendTunneled(removeNotifications);

        if ((GetQuestBadge(player, npc) ?? npc.Notification) is { } badge)
            player.SendTunneled(new PlayerUpdatePacketAddNotifications { Notifications = { badge } });
    }

    public NotificationInfo? GetQuestBadge(Player player, Npc npc)
    {
        var imageId = QuestBadgeImageId(player, npc);
        if (imageId == 0)
            return null;

        return new NotificationInfo
        {
            Guid = npc.Guid,
            IsCompact = false,
            IconState = imageId,
            NameId = npc.NameId,
            ReferenceId = npc.SubTextNameId,
            Enabled = false
        };
    }

    private int QuestBadgeImageId(Player player, Npc npc)
    {
        var quests = _resourceManager.Quests;

        if (quests.ByGiver.TryGetValue(npc.DefinitionId, out var giverQuestIds))
        {
            foreach (var questId in giverQuestIds)
            {
                if (quests.TryGet(questId, out var quest) && quest.IsOfferableFor(player.Quests))
                    return quest.NotificationAvailable;
            }
        }

        if (quests.ByTarget.TryGetValue(npc.DefinitionId, out var targetQuestIds))
        {
            foreach (var questId in targetQuestIds)
            {
                if (player.Quests.TryGetInProgress(questId, out var progress) && quests.TryGet(questId, out var quest)
                    && quest.IsWaitingOn(progress, npc))
                    return quest.NotificationActive;
            }
        }

        return 0;
    }

    public bool IsHiddenByQuest(Player player, Npc npc)
    {
        var quests = _resourceManager.Quests;

        if (!quests.ByStateNpc.TryGetValue(npc.DefinitionId, out var questIds))
            return false;

        foreach (var questId in questIds)
        {
            if (quests.TryGet(questId, out var quest) && quest.HidesNpcFor(player.Quests, npc.DefinitionId))
                return true;
        }

        return false;
    }

    public int GetQuestEffectId(Player player, Npc npc)
    {
        var quests = _resourceManager.Quests;

        if (!quests.ByStateNpc.TryGetValue(npc.DefinitionId, out var questIds))
            return 0;

        foreach (var questId in questIds)
        {
            if (quests.TryGet(questId, out var quest) && quest.NpcEffectIdFor(player.Quests, npc.DefinitionId) is var effectId and not 0)
                return effectId;
        }

        return 0;
    }

    private static void RefreshNpcEffects(Player player, QuestGoal goal)
    {
        foreach (var effect in goal.NpcEffectsUntilDone)
        {
            foreach (var npc in player.VisibleNpcs.Values.Where(npc => npc.DefinitionId == effect.NpcId).ToList())
            {
                player.OnRemoveVisibleNpcs(npc);
                player.OnAddVisibleNpcs(npc);
            }
        }
    }

    private HashSet<int> QuestBadgeIconStates(Npc npc)
    {
        var quests = _resourceManager.Quests;
        var states = new HashSet<int>();

        if (quests.ByGiver.TryGetValue(npc.DefinitionId, out var giverQuestIds))
        {
            foreach (var questId in giverQuestIds)
            {
                if (quests.TryGet(questId, out var quest) && quest.NotificationAvailable != 0)
                    states.Add(quest.NotificationAvailable);
            }
        }

        if (quests.ByTarget.TryGetValue(npc.DefinitionId, out var targetQuestIds))
        {
            foreach (var questId in targetQuestIds)
            {
                if (quests.TryGet(questId, out var quest) && quest.NotificationActive != 0)
                    states.Add(quest.NotificationActive);
            }
        }

        return states;
    }

    private const float DefaultOfferCameraDistance = 0f;

    private void Offer(Player player, QuestDefinition quest)
    {
        var offer = new QuestInfoPacket
        {
            QuestId = quest.QuestId,
            TitleId = quest.GiverDialogueId,
            DescriptionId = quest.DescriptionId,
            HelperTextId = quest.TitleId,
            IconId = quest.IconId,
            GoalsTextId = quest.ObjectiveDescriptionId,
            MembersOnly = false,
            NpcGuid = NearestNpcGuid(player, quest.GiverNpcId),
            CameraDistance = quest.CameraDistance ?? DefaultOfferCameraDistance,
            Unknown11 = false,
            AcceptOnly = false
        };

        FillRewardBundle(offer.RewardBundle, quest);

        player.SendTunneled(offer);
    }

    private void FillRewardBundle(RewardBundleBase bundle, QuestDefinition quest)
    {
        bundle.Success = false;
        bundle.Coins = quest.RewardCoins;
        bundle.Experience = quest.RewardExperience;
        bundle.Unknown3 = 0;
        bundle.Multiplier = 1f;
        bundle.IconId = -1;
        bundle.NameId = -1;

        foreach (var entry in BuildRewardEntries(quest))
            bundle.Entries.Add(entry);
    }

    private List<RewardBundleEntryBase> BuildRewardEntries(QuestDefinition quest)
    {
        var entries = new List<RewardBundleEntryBase>();
        foreach (var definitionId in quest.RewardItems)
        {
            if (_resourceManager.ClientItemDefinitions.TryGetValue(definitionId, out var itemDef))
            {
                entries.Add(new RewardBundleEntryItem
                {
                    IconId = itemDef.Icon.Id,
                    TintId = itemDef.Icon.TintId,
                    NameId = itemDef.NameId,
                    DefinitionId = definitionId,
                    Quantity = 1
                });
            }
        }

        if (quest.RewardCollectionId != 0 &&
            _resourceManager.Collections.TryGetValue(quest.RewardCollectionId, out var collectionDef))
        {
            entries.Add(new RewardBundleEntryCollectionAdd
            {
                IconId = collectionDef.IconId,
                TintId = collectionDef.IconTintId,
                NameId = collectionDef.NameId,
                CollectionId = collectionDef.Id
            });
        }

        return entries;
    }

    private static ulong NearestUntalkedTarget(Player player, TalkToNpcGoal goal, GoalProgress goalProgress)
    {
        var untalkedNpcIds = goal.AllTalkTargetNpcIds()
            .Where(npcId => goalProgress is not TalkProgress talkProgress || !talkProgress.HasTalkedTo(npcId))
            .ToHashSet();

        return NearestNpc(player, npc => untalkedNpcIds.Contains(npc.DefinitionId))?.Guid ?? 0;
    }

    private void CompleteGoal(Player player, QuestDefinition quest, QuestProgress progress, int goalIndex, Npc? spokenBy = null, bool playDialogue = true)
    {
        var goals = quest.Goals;
        var completedGoal = goals[goalIndex];

        var stepDone = progress.FinishGoal(quest, goalIndex);
        bool isFinalGoal = progress.GoalIndex >= goals.Count;

        player.SendTunneled(new QuestObjectiveCompletePacket
        {
            QuestId = quest.QuestId,
            ObjectiveId = completedGoal.NameId,
            Percent = QuestCompletion(quest, progress.GoalsDone),
            Silent = isFinalGoal
        });

        if (stepDone)
        {
            SaveQuestRow(player, quest.QuestId, q => q.CurrentGoalId = isFinalGoal ? AllGoalsDoneGoalId : goals[progress.GoalIndex].NameId);
            DeleteSavedGoalProgress(player, quest.QuestId);
        }
        else
        {
            SaveGoalDone(player, quest.QuestId, completedGoal.NameId);
        }

        RemoveHiddenNpcs(player, completedGoal);
        RefreshNpcEffects(player, completedGoal);
        RefreshQuestNotifications(player, quest);

        if (isFinalGoal)
        {
            TurnIn(player, quest);
            return;
        }

        if (stepDone)
        {
            foreach (var openGoalIndex in progress.OpenGoals.Keys.Order())
            {
                player.SendTunneled(new QuestObjectiveAddedPacket
                {
                    QuestId = quest.QuestId,
                    Objective = ObjectiveFor(goals[openGoalIndex], ObjectiveActive)
                });
                SendObjectiveActivated(player, quest.QuestId, goals[openGoalIndex], restored: false);
            }
        }

        SendObjectiveForGoal(player, quest, progress.FirstOpenGoalIndex);

        if (playDialogue && completedGoal is TalkToNpcGoal)
        {
            var speakerNpcId = spokenBy?.DefinitionId ?? quest.GoalTargetNpcId(goalIndex);
            var speakerGuid = spokenBy?.Guid ?? NearestNpcGuid(player, speakerNpcId);

            QuestDialogue.Begin(player, completedGoal.ConversationFor(speakerNpcId), speakerGuid);
        }
    }

    private const int YouGotItTextId = 103085;

    private const int GreenCheckImageId = 300;

    private const int GreenButtonImageSet = 17;

    private void TurnIn(Player player, QuestDefinition quest)
    {
        var end = new QuestEndPacket
        {
            NpcGuid = NearestNpcGuid(player, quest.GoalTargetNpcId(quest.Goals.Count - 1)),
            QuestId = quest.QuestId,
            TitleId = quest.TurnInDialogueId,
            DescriptionId = quest.TitleId
        };

        FillRewardBundle(end.RewardBundle, quest);

        player.SendTunneled(end);

        player.Quests.PendingTurnInQuestId = quest.QuestId;
    }

    private const int QuestListRewardUnknown3 = 15;
    private const int CoinsIconId = 102;
    private const int CoinsTextId = 6513;
    private const int ExperienceIconId = 101;

    private RewardBundleBase QuestListRewardBundle(Player player, QuestDefinition quest)
    {
        var bundle = new RewardBundleBase
        {
            Coins = quest.RewardCoins,
            Experience = quest.RewardExperience,
            Unknown3 = QuestListRewardUnknown3,
            PlayerGuid = player.Guid
        };

        bundle.Entries.AddRange(BuildRewardEntries(quest));

        if (bundle.Entries.Count > 0)
        {
            bundle.IconId = bundle.Entries[0].IconId;
            bundle.NameId = bundle.Entries[0].NameId;
        }
        else if (quest.RewardCoins > 0)
        {
            bundle.IconId = CoinsIconId;
            bundle.NameId = CoinsTextId;
        }
        else if (quest.RewardExperience > 0)
        {
            bundle.IconId = ExperienceIconId;
        }

        return bundle;
    }

    private ClientQuest NewClientQuest(Player player, QuestDefinition quest, QuestProgress progress, bool suppressStartBanner) => new()
    {
        QuestId = quest.QuestId,
        TitleId = quest.TitleId,
        DescriptionId = quest.ObjectiveDescriptionId,
        HelperTextId = quest.ObjectiveDescriptionId,
        MembersOnly = false,
        TimeStarted = progress.Started.ToUnixTimeSeconds(),
        ProfileId = 0,
        CompletedPercentage = QuestCompletion(quest, progress.GoalsDone),
        IconId = quest.IconId,
        SystemQuest = false,
        SuppressStartBanner = suppressStartBanner,
        RewardBundle = QuestListRewardBundle(player, quest)
    };

    private ClientQuest ClientQuestFor(Player player, QuestDefinition quest, QuestProgress progress)
    {
        var clientQuest = NewClientQuest(player, quest, progress, suppressStartBanner: true);
        var goals = quest.Goals;

        for (int i = 0; i < progress.StepEnd; i++)
        {
            clientQuest.Objectives[i + 1] = progress.IsGoalDone(i)
                ? ObjectiveFor(goals[i], ObjectiveComplete)
                : ObjectiveFor(goals[i], ObjectiveInactive, progress.ProgressFor(i).Count);
        }

        return clientQuest;
    }

    private void SendActiveState(Player player, QuestDefinition quest)
    {
        if (!player.Quests.TryGetInProgress(quest.QuestId, out var progress))
            return;

        player.SendTunneled(new QuestAddPacket
        {
            Quest = NewClientQuest(player, quest, progress, suppressStartBanner: false),
            ShowInTracker = quest.QuestId == player.Quests.ActiveQuestId
        });

        var goals = quest.Goals;

        for (int i = 0; i < progress.StepEnd; i++)
        {
            player.SendTunneled(new QuestObjectiveAddedPacket
            {
                QuestId = quest.QuestId,
                Objective = progress.IsGoalDone(i)
                    ? ObjectiveFor(goals[i], ObjectiveComplete)
                    : ObjectiveFor(goals[i], ObjectiveActive, progress.ProgressFor(i).Count)
            });
        }

        foreach (var goalIndex in progress.OpenGoals.Keys.Order())
            SendObjectiveActivated(player, quest.QuestId, goals[goalIndex], restored: false);

        SendObjectiveForGoal(player, quest, progress.FirstOpenGoalIndex);
    }

    private const int ObjectiveInactive = 0;
    private const int ObjectiveActive = 1;
    private const int ObjectiveComplete = 2;

    private static QuestObjectiveInfo ObjectiveFor(QuestGoal goal, int state, int currentCount = 0) => new()
    {
        ObjectiveId = goal.NameId,
        NameId = goal.NameId,
        DescriptionId = goal.DescriptionId != 0 ? goal.DescriptionId : goal.NameId,
        State = state,
        CurrentCount = state == ObjectiveComplete ? goal.RequiredCount : currentCount,
        RequiredCount = goal.RequiredCount
    };

    private static void SendObjectiveActivated(Player player, int questId, QuestGoal goal, bool restored = true)
    {
        player.SendTunneled(new QuestObjectiveActivatedPacket
        {
            QuestId = questId,
            ObjectiveId = goal.NameId,
            RequiredCount = restored ? goal.RequiredCount : 0,
            Unknown2 = restored
        });
    }

    private const int RetailObjectiveUpdateUnknown6 = 82;

    private static float QuestCompletion(QuestDefinition quest, int goalsDone) => (float)goalsDone / quest.Goals.Count;

    private static Npc? NearestNpc(Player player, int npcId) => NearestNpc(player, npc => npc.DefinitionId == npcId);

    private static Npc? NearestNpc(Player player, Func<Npc, bool> match)
    {
        Npc? nearest = null;
        var best = float.MaxValue;

        foreach (var npc in player.Zone.Npcs)
        {
            if (!match(npc))
                continue;

            var dx = npc.Position.X - player.Position.X;
            var dz = npc.Position.Z - player.Position.Z;
            var distance = dx * dx + dz * dz;

            if (distance < best)
            {
                best = distance;
                nearest = npc;
            }
        }

        return nearest;
    }

    private static ulong NearestNpcGuid(Player player, int npcId)
        => npcId != 0 && NearestNpc(player, npcId) is { } npc ? npc.Guid : 0;

    private ulong ResolveGoalTargetGuid(Player player, QuestDefinition quest, int goalIndex)
    {
        var goals = quest.Goals;

        if (goalIndex >= 0 && goalIndex < goals.Count
            && goals[goalIndex] is CollectGoal collectGoal)
        {
            var nearest = NearestCollectionNode(player, collectGoal.CollectNodeType);
            if (nearest is not null)
                return nearest.Guid;
        }

        if (goalIndex >= 0 && goalIndex < goals.Count && goals[goalIndex] is TalkToNpcGoal { IsCounted: true } countedGoal
            && player.Quests.TryGetInProgress(quest.QuestId, out var progress))
        {
            var untalked = NearestUntalkedTarget(player, countedGoal, progress.ProgressFor(goalIndex));
            if (untalked != 0)
                return untalked;
        }

        return NearestNpcGuid(player, quest.GoalTargetNpcId(goalIndex));
    }

    private static Npc? NearestCollectionNode(Player player, string nodeType)
        => string.IsNullOrEmpty(nodeType)
            ? null
            : NearestNpc(player, npc => npc is CollectionNode node && node.TypeDefinition.Key == nodeType);
    private void SendObjectiveForGoal(Player player, QuestDefinition quest, int goalIndex)
    {
        var goals = quest.Goals;

        if (quest.HideObjectiveTarget)
        {
            player.SendTunneled(new ObjectiveTargetUpdatePacket { Active = false });
            return;
        }

        if (goalIndex >= 0 && goalIndex < goals.Count
            && goals[goalIndex] is ReachLocationGoal reachGoal && reachGoal.ReachPosition != Vector3.Zero)
        {
            if (reachGoal.ZoneDefinitionId != player.Zone.DefinitionId)
            {
                player.SendTunneled(new ObjectiveTargetUpdatePacket { Active = false });
                return;
            }

            var reachPos = new Vector4(reachGoal.ReachPosition, 1f);

            player.SendTunneled(new ObjectiveTargetUpdatePacket
            {
                Active = true,
                TaskId = player.ObjectiveTaskId,
                Guid = 0,
                NameId = goals[goalIndex].NameId,
                PositionX = reachPos.X,
                PositionY = reachPos.Y,
                PositionZ = reachPos.Z,
                PositionW = 1f
            });
            return;
        }

        SendObjectiveTarget(player, ResolveGoalTargetGuid(player, quest, goalIndex));
    }

    private void SendObjectiveTarget(Player player, ulong targetGuid)
    {
        if (targetGuid == 0 || !player.Zone.TryGetNpc(targetGuid, out var target))
            return;

        var pos = target.Position;

        player.SendTunneled(new ObjectiveTargetUpdatePacket
        {
            Active = true,
            TaskId = player.ObjectiveTaskId,
            Guid = targetGuid,
            NameId = target.NameId,
            PositionX = pos.X,
            PositionY = pos.Y,
            PositionZ = pos.Z,
            PositionW = 1f
        });
    }

    public void RefreshObjectiveTarget(Player player)
    {
        if (TryGetTrackedGoal(player, out var quest, out var goalIndex))
            SendObjectiveForGoal(player, quest, goalIndex);
        else
            player.SendTunneled(new ObjectiveTargetUpdatePacket { Active = false });
    }

    private bool TryGetTrackedGoal(Player player, out QuestDefinition quest, out int goalIndex)
    {
        var activeQuestId = player.Quests.ActiveQuestId;

        if (activeQuestId != 0 && TryGetTrackableGoal(player, activeQuestId, out quest, out goalIndex))
            return true;

        quest = null!;
        goalIndex = -1;
        return false;
    }

    private bool TryGetTrackableGoal(Player player, int questId, out QuestDefinition quest, out int goalIndex)
    {
        quest = null!;
        goalIndex = -1;
        if (!_resourceManager.Quests.TryGet(questId, out var q) || !player.Quests.TryGetInProgress(questId, out var progress))
            return false;

        int currentGoalIndex = progress.FirstOpenGoalIndex;
        var goals = q.Goals;

        if (q.HideObjectiveTarget)
            return false;

        if (currentGoalIndex >= 0 && currentGoalIndex < goals.Count
            && goals[currentGoalIndex] is ReachLocationGoal reachGoal && reachGoal.ReachPosition != Vector3.Zero)
        {
            if (reachGoal.ZoneDefinitionId != player.Zone.DefinitionId)
                return false;

            quest = q;
            goalIndex = currentGoalIndex;
            return true;
        }

        ulong guid = ResolveGoalTargetGuid(player, q, currentGoalIndex);
        if (guid != 0 && player.Zone.TryGetNpc(guid, out _))
        {
            quest = q;
            goalIndex = currentGoalIndex;
            return true;
        }
        return false;
    }

    private void GrantReward(Player player, QuestDefinition quest)
    {
        var coins = quest.RewardCoins;
        if (coins > 0)
            _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, coins);

        var experience = quest.RewardExperience;

        var grantedCollection = quest.RewardCollectionId != 0 &&
            _resourceManager.Collections.TryGetValue(quest.RewardCollectionId, out var rewardedCollection)
                ? rewardedCollection
                : null;

        if (coins > 0 || experience > 0 || grantedCollection is not null)
        {
            var celebration = new RewardBundlePacket();

            // Success also gates whether each entry's tail is written; only the actual
            // collection id needs one here, so only flip it when there is a collection to report.
            celebration.RewardBundle.Success = grantedCollection is not null;
            celebration.RewardBundle.Coins = coins;
            celebration.RewardBundle.Experience = experience;
            celebration.RewardBundle.Unknown3 = 0;
            celebration.RewardBundle.Multiplier = 1f;
            celebration.RewardBundle.IconId = -1;
            celebration.RewardBundle.NameId = -1;

            if (grantedCollection is not null)
            {
                celebration.RewardBundle.Entries.Add(new RewardBundleEntryCollectionAdd
                {
                    IconId = grantedCollection.IconId,
                    TintId = grantedCollection.IconTintId,
                    NameId = grantedCollection.NameId,
                    CollectionId = grantedCollection.Id
                });
            }

            player.SendTunneled(celebration);
        }

        foreach (var itemDefinitionId in quest.RewardItems)
        {
            if (_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition))
                _rewardManager.TryGrantItem(player, itemDefinitionId, itemDefinition.IsTintable ? 0 : itemDefinition.Icon.TintId);
        }
    }
}
