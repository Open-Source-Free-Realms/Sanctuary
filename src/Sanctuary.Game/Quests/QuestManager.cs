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

    private IEnumerable<(QuestDefinition Quest, QuestProgress Progress, QuestGoal Goal)> ActiveGoals(Player player)
    {
        foreach (var progress in player.Quests.InProgress)
        {
            if (!_resourceManager.Quests.TryGet(progress.QuestId, out var quest) || progress.GoalIndex >= quest.Goals.Count)
                continue;

            yield return (quest, progress, quest.Goals[progress.GoalIndex]);
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
        => progress.GoalIndex < quest.Goals.Count && quest.Goals[progress.GoalIndex].Matches(quest, new NpcTalkedEvent(npc));

    private static bool TurnsInHere(QuestDefinition quest, QuestProgress progress, Npc npc)
        => progress.GoalIndex >= quest.Goals.Count && GoalTargetNpcId(quest, quest.Goals.Count - 1) == npc.DefinitionId;

    private void AdvanceAtNpc(Player player, QuestDefinition quest, Npc npc)
    {
        if (!player.Quests.TryGetInProgress(quest.QuestId, out var progress))
            return;

        if (TurnsInHere(quest, progress, npc))
        {
            TurnIn(player, quest);
            return;
        }

        if (!AdvancesHere(quest, progress, npc))
            return;

        var goal = quest.Goals[progress.GoalIndex];
        var result = goal.Progress(quest, new NpcTalkedEvent(npc), progress.Goal);

        ApplyProgress(player, quest, progress, result, npc);

        if (result != QuestGoalResult.Completed && goal is TalkToNpcGoal { IsCounted: true })
            QuestDialogue.Begin(player, goal.ConversationFor(npc.DefinitionId), npc.Guid);
    }

    public void OnEvent(Player player, QuestEvent questEvent)
    {
        foreach (var (quest, progress, goal) in ActiveGoals(player).ToList())
        {
            var result = goal.Progress(quest, questEvent, progress.Goal);

            if (result == QuestGoalResult.None)
                continue;

            ApplyProgress(player, quest, progress, result);

            if (questEvent.CreditsOneQuest)
                return;
        }
    }

    private void ApplyProgress(Player player, QuestDefinition quest, QuestProgress progress, QuestGoalResult result, Npc? npc = null)
    {
        if (result == QuestGoalResult.Completed)
        {
            CompleteGoal(player, quest, progress, npc);
            return;
        }

        if (result == QuestGoalResult.Progressed)
        {
            var goal = quest.Goals[progress.GoalIndex];

            player.SendTunneled(new QuestObjectiveUpdatePacket
            {
                QuestId = quest.QuestId,
                ObjectiveId = goal.NameId,
                CurrentCount = progress.Goal.Count,
                CompletedPercentage = QuestCompletion(quest, progress.GoalIndex),
                Unknown6 = RetailObjectiveUpdateUnknown6
            });

            SaveGoalProgress(player, quest.QuestId, goal.NameId, progress.Goal);

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
        progress.StartGoal(quest, 0);

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
                CurrentGoalId = quest.Goals[0].NameId
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

    public bool SkipGoal(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest)
            || !player.Quests.TryGetInProgress(questId, out var progress)
            || progress.GoalIndex >= quest.Goals.Count)
            return false;

        CompleteGoal(player, quest, progress);
        return true;
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

            int currentGoalIndex = progress.GoalIndex;
            var goals = quest.Goals;

            if (currentGoalIndex < goals.Count)
                SendObjectiveActivated(player, questId, goals[currentGoalIndex]);

            SendObjectiveForGoal(player, quest, currentGoalIndex);
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
                questData.Quests.Add(ClientQuestFor(quest, progress));
        }

        return questData;
    }

    public void RestoreJournal(Player player)
    {
        foreach (var (quest, _, goal) in ActiveGoals(player))
            SendObjectiveActivated(player, quest.QuestId, goal);

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
        RefreshQuestNotifications(player, quest.GiverNpcId);
        RefreshQuestNotifications(player, quest.TargetNpcId);

        foreach (var excludedId in quest.ExcludesQuestIds)
        {
            if (!_resourceManager.Quests.TryGet(excludedId, out var excludedQuest))
                continue;

            RefreshQuestNotifications(player, excludedQuest.GiverNpcId);
            RefreshQuestNotifications(player, excludedQuest.TargetNpcId);
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
                if (player.Quests.TryGetInProgress(questId, out _) && quests.TryGet(questId, out var quest))
                    return quest.NotificationActive;
            }
        }

        return 0;
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

    private const float DefaultOfferCameraDistance = 6f;

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
                    NameId = itemDef.NameId,
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

    private static ulong NearestUntalkedTarget(Player player, TalkToNpcGoal goal, QuestProgress progress)
    {
        var untalkedNpcIds = goal.AllTalkTargetNpcIds()
            .Where(npcId => progress.Goal is not TalkProgress talkProgress || !talkProgress.HasTalkedTo(npcId))
            .ToHashSet();

        return NearestNpc(player, npc => untalkedNpcIds.Contains(npc.DefinitionId))?.Guid ?? 0;
    }

    private void CompleteGoal(Player player, QuestDefinition quest, QuestProgress progress, Npc? spokenBy = null)
    {
        var goals = quest.Goals;
        var completedGoalIndex = progress.GoalIndex;
        var completedGoal = goals[completedGoalIndex];

        progress.Advance(quest);

        bool isFinalGoal = progress.GoalIndex >= goals.Count;

        player.SendTunneled(new QuestObjectiveCompletePacket
        {
            QuestId = quest.QuestId,
            ObjectiveId = completedGoal.NameId,
            Percent = QuestCompletion(quest, progress.GoalIndex),
            Silent = isFinalGoal
        });

        SaveQuestRow(player, quest.QuestId, q => q.CurrentGoalId = isFinalGoal ? AllGoalsDoneGoalId : goals[progress.GoalIndex].NameId);
        DeleteSavedGoalProgress(player, quest.QuestId);

        if (isFinalGoal)
        {
            TurnIn(player, quest);
            return;
        }

        player.SendTunneled(new QuestObjectiveAddedPacket
        {
            QuestId = quest.QuestId,
            Objective = ObjectiveFor(goals[progress.GoalIndex], ObjectiveActive)
        });
        SendObjectiveActivated(player, quest.QuestId, goals[progress.GoalIndex], restored: false);
        SendObjectiveForGoal(player, quest, progress.GoalIndex);

        if (completedGoal is TalkToNpcGoal)
        {
            var speakerNpcId = spokenBy?.DefinitionId ?? GoalTargetNpcId(quest, completedGoalIndex);
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
            NpcGuid = NearestNpcGuid(player, GoalTargetNpcId(quest, quest.Goals.Count - 1)),
            QuestId = quest.QuestId,
            TitleId = quest.TurnInDialogueId,
            DescriptionId = quest.TitleId
        };

        FillRewardBundle(end.RewardBundle, quest);

        player.SendTunneled(end);

        player.Quests.PendingTurnInQuestId = quest.QuestId;
    }

    private static ClientQuest NewClientQuest(QuestDefinition quest, float completedPercentage, bool suppressStartBanner) => new()
    {
        QuestId = quest.QuestId,
        TitleId = quest.TitleId,
        DescriptionId = quest.ObjectiveDescriptionId,
        HelperTextId = quest.ObjectiveDescriptionId,
        MembersOnly = false,
        TimeStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ProfileId = 0,
        CompletedPercentage = completedPercentage,
        IconId = quest.IconId,
        SystemQuest = false,
        SuppressStartBanner = suppressStartBanner
    };

    private static ClientQuest ClientQuestFor(QuestDefinition quest, QuestProgress progress)
    {
        var clientQuest = NewClientQuest(quest, QuestCompletion(quest, progress.GoalIndex), suppressStartBanner: true);
        var goals = quest.Goals;
        int lastVisible = System.Math.Min(progress.GoalIndex, goals.Count - 1);

        for (int i = 0; i <= lastVisible; i++)
        {
            clientQuest.Objectives[i + 1] = i < progress.GoalIndex
                ? ObjectiveFor(goals[i], ObjectiveComplete)
                : ObjectiveFor(goals[i], ObjectiveInactive, progress.Goal.Count);
        }

        return clientQuest;
    }

    private void SendActiveState(Player player, QuestDefinition quest)
    {
        if (!player.Quests.TryGetInProgress(quest.QuestId, out var progress))
            return;

        int goalsDone = progress.GoalIndex;
        player.SendTunneled(new QuestAddPacket
        {
            Quest = NewClientQuest(quest, QuestCompletion(quest, goalsDone), suppressStartBanner: false),
            ShowInTracker = quest.QuestId == player.Quests.ActiveQuestId
        });

        var goals = quest.Goals;
        int lastVisible = System.Math.Min(goalsDone, goals.Count - 1);

        int collected = progress.Goal.Count;

        for (int i = 0; i <= lastVisible; i++)
        {
            player.SendTunneled(new QuestObjectiveAddedPacket
            {
                QuestId = quest.QuestId,
                Objective = i < goalsDone
                    ? ObjectiveFor(goals[i], ObjectiveComplete)
                    : ObjectiveFor(goals[i], ObjectiveActive, collected)
            });
        }

        if (goalsDone < goals.Count)
            SendObjectiveActivated(player, quest.QuestId, goals[goalsDone], restored: false);

        SendObjectiveForGoal(player, quest, goalsDone);
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
        CurrentCount = currentCount,
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

    private static int GoalTargetNpcId(QuestDefinition quest, int goalIndex)
    {
        var goals = quest.Goals;
        return goalIndex >= 0 && goalIndex < goals.Count && goals[goalIndex] is TalkToNpcGoal talkGoal
            ? talkGoal.TargetNpcId(quest)
            : quest.TargetNpcId;
    }

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
            var untalked = NearestUntalkedTarget(player, countedGoal, progress);
            if (untalked != 0)
                return untalked;
        }

        return NearestNpcGuid(player, GoalTargetNpcId(quest, goalIndex));
    }

    private static Npc? NearestCollectionNode(Player player, string nodeType)
        => string.IsNullOrEmpty(nodeType)
            ? null
            : NearestNpc(player, npc => npc is CollectionNode node && node.TypeDefinition.Key == nodeType);
    private void SendObjectiveForGoal(Player player, QuestDefinition quest, int goalIndex)
    {
        var goals = quest.Goals;

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

        int currentGoalIndex = progress.GoalIndex;
        var goals = q.Goals;

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
