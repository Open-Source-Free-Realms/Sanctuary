using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Quests;

public sealed class QuestManager : IQuestManager
{
    public const int AllGoalsDoneGoalId = 0;

    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ILogger<QuestManager> _logger;

    public QuestManager(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, ILogger<QuestManager> logger)
    {
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public bool IsQuestNpc(int npcId)
        => _resourceManager.Quests.ByGiver.ContainsKey(npcId) || _resourceManager.Quests.ByTarget.ContainsKey(npcId);

    private IEnumerable<(int QuestId, QuestDefinition Quest, int GoalIndex, QuestGoal Goal)> ActiveGoals(Player player)
    {
        foreach (var (questId, completed) in player.Quests)
        {
            if (completed || !_resourceManager.Quests.TryGet(questId, out var quest))
                continue;

            var goals = quest.Goals;
            int done = player.QuestGoalProgress.TryGetValue(questId, out var progress) ? progress : 0;
            if (done >= goals.Count)
                continue;

            yield return (questId, quest, done, goals[done]);
        }
    }

    public List<NpcInteractionOption> GetInteractionOptions(Player player, Npc npc)
    {
        var options = new List<NpcInteractionOption>();
        var quests = _resourceManager.Quests;

        foreach (var (questId, completed) in player.Quests)
        {
            if (completed || !quests.TryGet(questId, out var activeQuest))
                continue;

            if (!AdvancesHere(player, activeQuest, npc, out _) && !TurnsInHere(player, activeQuest, npc))
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

    private bool AdvancesHere(Player player, QuestDefinition quest, Npc npc, out int goalIndex)
    {
        goalIndex = player.QuestGoalProgress.TryGetValue(quest.QuestId, out var progress) ? progress : 0;

        var goals = quest.Goals;

        if (goalIndex >= goals.Count)
            return false;

        if (goals[goalIndex] is CollectGoal)
            return false;

        if (goals[goalIndex] is TalkToNpcGoal { IsCounted: true } countedGoal)
            return countedGoal.AllTalkTargetNpcIds().Contains(npc.DefinitionId);

        return GoalTargetNpcId(quest, goalIndex) == npc.DefinitionId;
    }

    private static bool TurnsInHere(Player player, QuestDefinition quest, Npc npc)
        => player.QuestGoalProgress.TryGetValue(quest.QuestId, out var done) && done >= quest.Goals.Count
            && GoalTargetNpcId(quest, quest.Goals.Count - 1) == npc.DefinitionId;

    private void AdvanceAtNpc(Player player, QuestDefinition quest, Npc npc)
    {
        if (TurnsInHere(player, quest, npc))
        {
            TurnIn(player, quest);
            return;
        }

        if (!AdvancesHere(player, quest, npc, out var goalIndex))
            return;

        if (quest.Goals[goalIndex] is TalkToNpcGoal { IsCounted: true } countedGoal)
            TryCreditCountedTalk(player, quest, goalIndex, countedGoal, npc);
        else
            CompleteGoal(player, quest, goalIndex, npc);
    }

    public void OnNpcInteract(Player player, Npc npc)
    {
        var options = GetInteractionOptions(player, npc);

        if (options.Count > 1)
        {
            player.SendInteractionMenu(npc, options);
            return;
        }

        if (options.Count == 1)
            options[0].Invoke(player);
    }

    private const int CollectPickupEffect = 5386;

    public void OnCollectionNodeGathered(Player player, CollectionNode node)
    {
        var nodeType = node.TypeDefinition.Key;

        foreach (var (questId, quest, goalIndex, activeGoal) in ActiveGoals(player))
        {
            if (activeGoal is not CollectGoal goal || goal.CollectNodeType != nodeType)
                continue;

            var required = goal.RequiredCount;
            if (required <= 0)
                return;

            var count = (player.QuestCollectProgress.TryGetValue(questId, out var c) ? c : 0) + 1;

            if (count >= required)
            {
                player.QuestCollectProgress.Remove(questId);
                CompleteGoal(player, quest, goalIndex);
                return;
            }

            player.QuestCollectProgress[questId] = count;

            player.SendTunneled(new QuestObjectiveUpdatePacket
            {
                QuestId = questId,
                ObjectiveId = goal.NameId,
                CurrentCount = count,
                CompletedPercentage = (float)count / required
            });

            PersistGoalProgress(player, questId, goal.NameId, 0, count);
            RefreshObjectiveTarget(player);
            return;
        }
    }
    private const float DefaultReachRadius = 12f;

    public void OnPlayerMoved(Player player)
    {
        foreach (var (questId, quest, done, activeGoal) in ActiveGoals(player))
        {
            if (activeGoal is not ReachLocationGoal goal || goal.ReachPosition == Vector3.Zero)
                continue;

            var radius = goal.ReachRadius > 0 ? goal.ReachRadius : DefaultReachRadius;
            var target = goal.ReachPosition;

            if (!player.Position.IsInCircle(target, radius))
                continue;

            CompleteGoal(player, quest, done);
        }
    }

    private void UpdateCharacterQuest(Player player, int questId, Action<DbCharacterQuest> update)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var dbQuest = db.CharacterQuests.FirstOrDefault(x => x.QuestId == questId && x.CharacterId == player.CharacterId);
        if (dbQuest is null)
            return;

        update(dbQuest);
        db.SaveChanges();
    }

    private void PersistGoalProgress(Player player, int questId, int goalId, ulong targetId, int count)
    {
        using var db = _dbContextFactory.CreateDbContext();

        var progress = db.CharacterQuestProgress.FirstOrDefault(p => p.CharacterId == player.CharacterId
            && p.QuestId == questId && p.GoalId == goalId && p.TargetId == targetId);

        if (progress is null)
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
        else
        {
            progress.Count = count;
        }

        db.SaveChanges();
    }

    private void ClearGoalProgress(Player player, int questId)
    {
        using var db = _dbContextFactory.CreateDbContext();

        db.CharacterQuestProgress
            .Where(p => p.CharacterId == player.CharacterId && p.QuestId == questId)
            .ExecuteDelete();
    }

    private void PersistActiveQuest(Player player, int questId)
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

        player.Quests[questId] = false;
        player.QuestGoalProgress.Remove(questId);
        player.QuestCollectProgress.Remove(questId);
        ClearTalkProgress(player, quest);
        player.ActiveQuestId = questId;
        player.LastQuestAcceptedAt = DateTime.UtcNow;

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

    public void CompleteQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest))
            return;

        if (player.Quests.TryGetValue(questId, out var done) && done)
            return;

        player.Quests[questId] = true;
        player.QuestCollectProgress.Remove(questId);
        ClearTalkProgress(player, quest);
        UpdateCharacterQuest(player, questId, q => q.Completed = true);

        if (player.ActiveQuestId == questId)
        {
            player.ActiveQuestId = 0;
            PersistActiveQuest(player, 0);
        }

        player.SendTunneled(new QuestCompletePacket { QuestId = questId });

        player.SendTunneled(new CompletedQuestCountUpdatePacket
        {
            Count = player.Quests.Values.Count(done => done)
        });

        SendJournalQuestStates(player);

        GrantReward(player, quest);

        RefreshQuestNotifications(player, quest);

        if (quest.NextQuestId is { } nextQuestId && _resourceManager.Quests.TryGet(nextQuestId, out var next))
            RefreshQuestNotifications(player, next.GiverNpcId);

        RefreshObjectiveTarget(player);
    }

    public void AbandonQuest(Player player, int questId)
    {
        if ((DateTime.UtcNow - player.LastQuestAcceptedAt).TotalSeconds < 3)
            return;

        if (!(player.Quests.TryGetValue(questId, out var completed) && !completed))
        {
            var active = player.Quests.Where(entry => !entry.Value).Select(entry => entry.Key).ToList();
            if (active.Count != 1)
                return;

            questId = active[0];
        }

        if (!_resourceManager.Quests.TryGet(questId, out var quest))
            return;

        player.Quests.Remove(questId);
        player.QuestCollectProgress.Remove(questId);
        ClearTalkProgress(player, quest);
        QuestDialogue.Clear(player);

        var wasActive = player.ActiveQuestId == questId;
        if (wasActive)
            player.ActiveQuestId = 0;

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

        if (player.Quests.TryGetValue(questId, out var completed) && !completed)
        {
            player.ActiveQuestId = questId;
            PersistActiveQuest(player, questId);

            int done = player.QuestGoalProgress.TryGetValue(questId, out var progress) ? progress : 0;
            var goals = quest.Goals;

            if (done < goals.Count)
                SendObjectiveActivated(player, questId, goals[done]);

            SendObjectiveForGoal(player, quest, done);
        }
    }

    public void RestoreJournal(Player player)
    {
        foreach (var (questId, completed) in player.Quests)
        {
            if (!completed && _resourceManager.Quests.TryGet(questId, out var quest))
                SendActiveState(player, quest, sendObjectiveTarget: false, suppressStartBanner: true);
        }

        RefreshObjectiveTarget(player);

        player.SendTunneled(new CompletedQuestCountUpdatePacket
        {
            Count = player.Quests.Values.Count(done => done)
        });

        SendJournalQuestStates(player);
    }

    private void SendJournalQuestStates(Player player)
    {
        var states = new Dictionary<int, int>();
        foreach (var (questId, completed) in player.Quests)
            if (completed)
                states[questId] = 1;

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
        if (npcGuid == 0 || !player.Zone.TryGetNpc(npcGuid, out var npc))
            return;

        var imageId = player.GetNotificationImageId(npc);

        player.SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = npc.Guid });

        var addNpcPacket = npc.GetAddNpcPacket();
        addNpcPacket.NotificationImageSetId = imageId;
        player.SendTunneled(addNpcPacket);

        if (npc.CursorId != 0)
        {
            var relevance = new PlayerUpdatePacketNpcRelevance();
            relevance.Entries.Add(new PlayerUpdatePacketNpcRelevance.Entry
            {
                Guid = npc.Guid,
                HasCursor = true,
                CursorId = npc.CursorId,
                Unknown2 = imageId != 0
            });
            player.SendTunneled(relevance);
        }

        if (imageId == 0)
        {
            player.SendTunneled(new PlayerUpdatePacketRemoveNotifications { Notifications = { new RemoveNotificationInfo { Guid = npc.Guid } } });
            return;
        }

        player.SendTunneled(new PlayerUpdatePacketAddNotifications
        {
            Notifications =
            {
                new NotificationInfo
                {
                    Guid = npc.Guid,
                    IsCompact = false,
                    IconState = imageId,
                    NameId = npc.NameId,
                    ReferenceId = npc.SubTextNameId,
                    Enabled = false,
                }
            }
        });
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

    private bool TryCreditCountedTalk(Player player, QuestDefinition quest, int goalIndex, TalkToNpcGoal goal, Npc npc)
    {
        if (!goal.AllTalkTargetNpcIds().Contains(npc.DefinitionId))
            return false;

        bool alreadyCredited = !player.TalkedQuestNpcs.Add(npc.DefinitionId);

        if (!alreadyCredited)
            RefreshQuestNotification(player, npc.Guid);

        int required = goal.RequiredCount;
        int count = player.QuestCollectProgress.TryGetValue(quest.QuestId, out var c) ? c : 0;

        if (!alreadyCredited)
            count++;

        if (!alreadyCredited && count >= required)
        {
            player.QuestCollectProgress.Remove(quest.QuestId);
            ClearTalkProgress(player, goal);
            CompleteGoal(player, quest, goalIndex, npc);
            return true;
        }

        if (!alreadyCredited)
        {
            player.QuestCollectProgress[quest.QuestId] = count;

            player.SendTunneled(new QuestObjectiveUpdatePacket
            {
                QuestId = quest.QuestId,
                ObjectiveId = goal.NameId,
                CurrentCount = count,
                CompletedPercentage = (float)count / required
            });

            PersistGoalProgress(player, quest.QuestId, goal.NameId, (ulong)npc.DefinitionId, 1);
        }

        QuestDialogue.Begin(player, goal.ConversationFor(npc.DefinitionId), npc.Guid);

        RefreshObjectiveTarget(player);
        return true;
    }

    private static void ClearTalkProgress(Player player, TalkToNpcGoal goal)
    {
        foreach (var npcId in goal.AllTalkTargetNpcIds())
            player.TalkedQuestNpcs.Remove(npcId);
    }

    private static void ClearTalkProgress(Player player, QuestDefinition quest)
    {
        foreach (var goal in quest.Goals)
            if (goal is TalkToNpcGoal { IsCounted: true } countedGoal)
                ClearTalkProgress(player, countedGoal);
    }

    private static ulong NearestUntalkedTarget(Player player, TalkToNpcGoal goal)
    {
        ulong nearest = 0;
        var best = float.MaxValue;

        foreach (var npcId in goal.AllTalkTargetNpcIds())
        {
            if (player.TalkedQuestNpcs.Contains(npcId))
                continue;

            if (NearestNpc(player, npcId) is not { } npc)
                continue;

            var dx = npc.Position.X - player.Position.X;
            var dz = npc.Position.Z - player.Position.Z;
            var distance = dx * dx + dz * dz;

            if (distance < best)
            {
                best = distance;
                nearest = npc.Guid;
            }
        }

        return nearest;
    }
    private void CompleteGoal(Player player, QuestDefinition quest, int goalIndex, Npc? spokenBy = null)
    {
        var goals = quest.Goals;

        bool isFinalGoal = goalIndex + 1 >= goals.Count;

        player.SendTunneled(new QuestObjectiveCompletePacket
        {
            QuestId = quest.QuestId,
            ObjectiveId = goals[goalIndex].NameId,
            Percent = 1f,
            Silent = isFinalGoal
        });

        int done = goalIndex + 1;
        player.QuestGoalProgress[quest.QuestId] = done;

        UpdateCharacterQuest(player, quest.QuestId, q => q.CurrentGoalId = done < goals.Count ? goals[done].NameId : AllGoalsDoneGoalId);
        ClearGoalProgress(player, quest.QuestId);

        if (done >= goals.Count)
        {
            TurnIn(player, quest);
            return;
        }

        player.SendTunneled(new QuestObjectiveAddedPacket
        {
            QuestId = quest.QuestId,
            Objective = ObjectiveFor(goals[done], ObjectiveActive)
        });
        SendObjectiveForGoal(player, quest, done);

        var completedGoal = goals[goalIndex];
        if (completedGoal is TalkToNpcGoal)
        {
            var speakerNpcId = spokenBy?.DefinitionId ?? GoalTargetNpcId(quest, goalIndex);
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

        player.PendingQuestEndAction = () => CompleteQuest(player, quest.QuestId);
    }

    private static void SendQuestAdd(Player player, QuestDefinition quest, int helperTextId, float completedPercentage = 0f, bool suppressStartBanner = false, bool showInTracker = false)
    {
        player.SendTunneled(new QuestAddPacket
        {
            QuestId = quest.QuestId,
            TitleId = quest.TitleId,
            DescriptionId = quest.ObjectiveDescriptionId,
            HelperTextId = helperTextId,
            MembersOnly = false,
            TimeStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ProfileId = 0,
            CompletedPercentage = completedPercentage,
            IconId = quest.IconId,
            SystemQuest = false,
            SuppressStartBanner = suppressStartBanner,
            ShowInTracker = showInTracker
        });
    }

    private void SendActiveState(Player player, QuestDefinition quest, bool sendObjectiveTarget = true, bool suppressStartBanner = false)
    {
        int alreadyDone = player.QuestGoalProgress.TryGetValue(quest.QuestId, out var p) ? p : 0;
        SendQuestAdd(player, quest, quest.ObjectiveDescriptionId, (float)alreadyDone / quest.Goals.Count, suppressStartBanner,
            showInTracker: quest.QuestId == player.ActiveQuestId);

        var goals = quest.Goals;
        int done = player.QuestGoalProgress.TryGetValue(quest.QuestId, out var progress) ? progress : 0;
        int lastVisible = System.Math.Min(done, goals.Count - 1);

        int collected = player.QuestCollectProgress.TryGetValue(quest.QuestId, out var count) ? count : 0;

        for (int i = 0; i <= lastVisible; i++)
        {
            player.SendTunneled(new QuestObjectiveAddedPacket
            {
                QuestId = quest.QuestId,
                Objective = i < done
                    ? ObjectiveFor(goals[i], ObjectiveComplete)
                    : ObjectiveFor(goals[i], ObjectiveActive, collected)
            });
        }

        if (sendObjectiveTarget)
            SendObjectiveForGoal(player, quest, done);
    }

    private const int ObjectiveActive = 1;
    private const int ObjectiveComplete = 2;

    private static QuestObjectiveInfo ObjectiveFor(QuestGoal goal, int state, int currentCount = 0) => new()
    {
        ObjectiveId = goal.NameId,
        NameId = goal.NameId,
        DescriptionId = goal.DescriptionId != 0 ? goal.DescriptionId : goal.NameId,
        State = state,
        CurrentCount = currentCount,
        RequiredCount = RequiredCountOf(goal)
    };

    private static int RequiredCountOf(QuestGoal goal) => goal switch
    {
        TalkToNpcGoal talkGoal => talkGoal.RequiredCount,
        CollectGoal collectGoal => collectGoal.RequiredCount,
        _ => 0
    };

    private static void SendObjectiveActivated(Player player, int questId, QuestGoal goal)
    {
        player.SendTunneled(new QuestObjectiveActivatedPacket
        {
            QuestId = questId,
            ObjectiveId = goal.NameId,
            RequiredCount = RequiredCountOf(goal),
            Unknown2 = false
        });
    }

    private static Npc? NearestNpc(Player player, int npcId)
    {
        Npc? nearest = null;
        var best = float.MaxValue;

        foreach (var npc in player.Zone.Npcs)
        {
            if (npc.DefinitionId != npcId)
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
        if (goalIndex >= 0 && goalIndex < goals.Count && goals[goalIndex] is TalkToNpcGoal talkGoal
            && talkGoal.AllTalkTargetNpcIds().FirstOrDefault() is var targetNpcId and not 0)
            return targetNpcId;
        return quest.TargetNpcId;
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

        if (goalIndex >= 0 && goalIndex < goals.Count && goals[goalIndex] is TalkToNpcGoal { IsCounted: true } countedGoal)
        {
            var untalked = NearestUntalkedTarget(player, countedGoal);
            if (untalked != 0)
                return untalked;
        }

        return NearestNpcGuid(player, GoalTargetNpcId(quest, goalIndex));
    }

    private static CollectionNode? NearestCollectionNode(Player player, string nodeType)
    {
        if (string.IsNullOrEmpty(nodeType))
            return null;

        CollectionNode? nearest = null;
        var best = float.MaxValue;

        foreach (var npc in player.Zone.Npcs)
        {
            if (npc is not CollectionNode node || node.TypeDefinition.Key != nodeType)
                continue;

            var dx = node.Position.X - player.Position.X;
            var dz = node.Position.Z - player.Position.Z;
            var distance = dx * dx + dz * dz;

            if (distance < best)
            {
                best = distance;
                nearest = node;
            }
        }

        return nearest;
    }
    private void SendObjectiveForGoal(Player player, QuestDefinition quest, int goalIndex)
    {
        var goals = quest.Goals;

        if (goalIndex >= 0 && goalIndex < goals.Count
            && goals[goalIndex] is ReachLocationGoal reachGoal && reachGoal.ReachPosition != Vector3.Zero)
        {
            var reachPos = new Vector4(reachGoal.ReachPosition, 1f);
            var reachZoneId = player.Zone is WorldZone reachZone
                ? reachZone.GetZoneAreaId(reachPos)
                : player.Zone.Id;

            player.SendTunneled(new ObjectiveTargetUpdatePacket
            {
                Active = true,
                LocationX = reachPos.X,
                LocationZ = reachPos.Z,
                ZoneId = reachZoneId,
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
        var zoneAreaId = player.Zone is WorldZone worldZone
            ? worldZone.GetZoneAreaId(pos)
            : player.Zone.Id;

        player.SendTunneled(new ObjectiveTargetUpdatePacket
        {
            Active = true,
            LocationX = pos.X,
            LocationZ = pos.Z,
            ZoneId = zoneAreaId,
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
        if (player.ActiveQuestId != 0
            && player.Quests.TryGetValue(player.ActiveQuestId, out var activeCompleted) && !activeCompleted
            && TryGetTrackableGoal(player, player.ActiveQuestId, out quest, out goalIndex))
        {
            return true;
        }

        foreach (var (questId, completed) in player.Quests)
        {
            if (completed)
                continue;
            if (TryGetTrackableGoal(player, questId, out quest, out goalIndex))
                return true;
        }

        quest = null!;
        goalIndex = -1;
        return false;
    }

    private bool TryGetTrackableGoal(Player player, int questId, out QuestDefinition quest, out int goalIndex)
    {
        quest = null!;
        goalIndex = -1;
        if (!_resourceManager.Quests.TryGet(questId, out var q))
            return false;

        int done = player.QuestGoalProgress.TryGetValue(questId, out var progress) ? progress : 0;
        var goals = q.Goals;

        if (done >= 0 && done < goals.Count
            && goals[done] is ReachLocationGoal reachGoal && reachGoal.ReachPosition != Vector3.Zero)
        {
            quest = q;
            goalIndex = done;
            return true;
        }

        ulong guid = ResolveGoalTargetGuid(player, q, done);
        if (guid != 0 && player.Zone.TryGetNpc(guid, out _))
        {
            quest = q;
            goalIndex = done;
            return true;
        }
        return false;
    }

    private void GrantReward(Player player, QuestDefinition quest)
    {
        var coins = quest.RewardCoins;
        if (coins > 0)
        {
            int newTotal;
            using (var db = _dbContextFactory.CreateDbContext())
            {
                var dbCharacter = db.Characters.FirstOrDefault(c => c.Id == player.CharacterId);
                if (dbCharacter is null)
                    return;

                dbCharacter.Coins += coins;
                db.SaveChanges();
                newTotal = dbCharacter.Coins;
            }

            player.Coins = newTotal;
            player.SendTunneled(new ClientUpdatePacketCoinCount { Coins = newTotal });
        }

        var experience = quest.RewardExperience;
        if (experience > 0)
            player.AwardXp(experience);

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
            GrantItem(player, itemDefinitionId);

            player.SendTunneled(new RewardNonBundledItemPacket { ItemDefinitionId = itemDefinitionId, Quantity = 1 });
        }
    }

    private void GrantItem(Player player, int definitionId)
    {
        if (!_resourceManager.ClientItemDefinitions.TryGetValue(definitionId, out var itemDef))
            return;

        int tint = itemDef.IsTintable ? 0 : itemDef.Icon.TintId;

        int itemId, count;
        using (var db = _dbContextFactory.CreateDbContext())
        {
            var row = db.Characters
                .Where(c => c.Id == player.CharacterId)
                .Select(c => new
                {
                    Character = c,
                    Item = c.Items.FirstOrDefault(i => i.Definition == definitionId && i.Tint == tint),
                    NextId = c.Items.Max(i => (int?)i.Id) ?? 0
                })
                .FirstOrDefault();

            if (row is null)
                return;

            if (row.Item is not null)
            {
                row.Item.Count += 1;
                itemId = row.Item.Id;
                count = row.Item.Count;
            }
            else
            {
                var dbItem = new DbItem { Id = row.NextId + 1, Definition = definitionId, Tint = tint, Count = 1 };
                row.Character.Items.Add(dbItem);
                itemId = dbItem.Id;
                count = 1;
            }

            db.SaveChanges();
        }

        var clientItem = player.Items.FirstOrDefault(x => x.Definition == definitionId && x.Tint == tint);
        if (clientItem is not null)
        {
            clientItem.Count = count;
            player.SendTunneled(new ClientUpdatePacketItemUpdate { ItemGuid = clientItem.Id, Count = clientItem.Count });
        }
        else
        {
            clientItem = new ClientItem { Id = itemId, Tint = tint, Count = count, Definition = definitionId };
            player.Items.Add(clientItem);

            using var writer = new PacketWriter();
            clientItem.Serialize(writer);
            itemDef.Serialize(writer);
            player.SendTunneled(new ClientUpdatePacketItemAdd { Payload = writer.Buffer });
        }
    }
}
