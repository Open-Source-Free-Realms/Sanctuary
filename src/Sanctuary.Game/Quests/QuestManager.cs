using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Quests;

// Data-driven implementation of IQuestManager. Every packet sequence here is the one
// the previously-hardcoded "Introduce Yourself" flow used (verified in-game); only the source of the
// values changed - they now come from the QuestDefinition instead of constants.
public sealed class QuestManager : IQuestManager
{
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ILogger<QuestManager> _logger;

    public QuestManager(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, ILogger<QuestManager> logger)
    {
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public bool IsQuestNpc(ulong npcGuid)
        => _resourceManager.Quests.ByGiver.ContainsKey(npcGuid) || _resourceManager.Quests.ByTarget.ContainsKey(npcGuid);

    // Every in-progress quest's current goal, paired with its quest and index. Shared by the goal-event
    // handlers below (interact/collect/move) so each only has to filter by QuestGoalType.
    private IEnumerable<(int QuestId, QuestDefinition Quest, int GoalIndex, QuestGoal Goal)> ActiveGoals(Player player)
    {
        foreach (var (questId, completed) in player.Quests)
        {
            if (completed || !_resourceManager.Quests.TryGet(questId, out var quest))
                continue;

            var goals = quest.EffectiveGoals;
            int done = player.QuestGoalProgress.TryGetValue(questId, out var progress) ? progress : 0;
            if (done >= goals.Count)
                continue; // all goals already done (turn-in fires on the last goal, so this shouldn't linger)

            yield return (questId, quest, done, goals[done]);
        }
    }

    public void OnNpcInteract(Player player, Npc npc)
    {
        var quests = _resourceManager.Quests;

        // 1. Goal progression / turn-in: is this NPC the target of the ACTIVE goal of a quest the player
        // has active (accepted, not yet completed)? Talking to it ticks that goal off; the last goal hands
        // the quest in (end screen). Multi-goal quests can point intermediate goals at different NPCs, so we
        // check each active quest's current goal rather than only the quest's turn-in NPC.
        foreach (var (_, activeQuest, done, goal) in ActiveGoals(player))
        {
            // A Collect goal advances only via OnCollectInteract. Since it has no NPC target,
            // GoalTargetGuid would fall back to the quest's turn-in NPC - talking to it must NOT tick
            // the goal off (that would bypass the objective), so skip it here.
            if (goal.Type == QuestGoalType.Collect)
                continue;

            if (GoalTargetGuid(activeQuest, done) == npc.Guid)
            {
                CompleteGoal(player, activeQuest, done);
                return;
            }
        }

        // 2. Offer: is this NPC the giver of a quest the player can currently take?
        if (quests.ByGiver.TryGetValue(npc.Guid, out var giverQuestIds))
        {
            foreach (var questId in giverQuestIds)
            {
                if (quests.TryGet(questId, out var offerableQuest) && offerableQuest.IsOfferableFor(player.Quests))
                {
                    Offer(player, offerableQuest);
                    return;
                }
            }
        }
    }

    // Composite effect played on a collectible when picked up (PFX_sparkles-swirl_gold_treasure-reward).
    private const int CollectPickupEffect = 5386;

    // A collectible pickup was clicked. Credits the quest's active Collect goal (one per distinct pickup),
    // hides the pickup for this player, animates the tracker counter, and completes the goal - advancing to
    // the return step - once RequiredCount is reached.
    public void OnCollectInteract(Player player, Npc npc)
    {
        if (!_resourceManager.Quests.Collectibles.TryGetValue(npc.Guid, out var loc))
            return;

        var (questId, goalIndex) = loc;
        if (!_resourceManager.Quests.TryGet(questId, out var quest))
            return;

        // Must have this quest active (accepted, not completed) and be ON this goal (earlier goals done).
        if (!player.Quests.TryGetValue(questId, out var completed) || completed)
            return;

        int done = player.QuestGoalProgress.TryGetValue(questId, out var progress) ? progress : 0;
        if (done != goalIndex)
            return; // not the active goal yet (a prior goal is pending) or already collected past it

        var goal = quest.EffectiveGoals[goalIndex];
        if (goal.Type != QuestGoalType.Collect)
            return;

        int required = goal.RequiredCount > 0 ? goal.RequiredCount : goal.CollectSpawns.Count;
        if (required <= 0)
            return;

        int count = (player.QuestCollectProgress.TryGetValue(questId, out var c) ? c : 0) + 1;

        // Gold sparkle "reward" burst where the pickup is - immediate visual feedback that the collect
        // registered (plays before the removal so the effect's source actor still exists).
        player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = npc.Guid,
            CompositeEffectId = CollectPickupEffect,
            Position = npc.Position
        }, sendToSelf: true);

        // Hide this pickup for the collecting player so it can't be re-clicked. Collectibles are shared, so
        // other players still see it; a relog re-adds them all and restarts this goal's (in-memory) count.
        player.SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = npc.Guid });
        player.CollectedPickups.Add(npc.Guid); // so the marker skips it and points at the next tool

        if (count >= required)
        {
            player.QuestCollectProgress.Remove(questId);
            // Final pickup -> tick the goal's checkmark and advance to the return goal (or turn in). Reuses
            // the same completion path as talk-to-NPC goals.
            CompleteGoal(player, quest, goalIndex);
        }
        else
        {
            player.QuestCollectProgress[questId] = count;
            // Animate the tracker's "current/required" counter (the client stores CurrentCount at the
            // objective's row+0xd4 and re-renders "count/required").
            player.SendTunneled(new QuestObjectiveUpdatePacket
            {
                QuestId = questId,
                ObjectiveId = goal.NameId,
                CurrentCount = count,
                CompletedPercentage = (float)count / required
            });

            // Persist so a relog mid-collect resumes at this count (done after the visual so the DB write
            // doesn't delay the on-screen feedback).
            PersistCollectCount(player, questId, count);

            // Re-point the marker/breadcrumb at the NEXT nearest uncollected pickup.
            RefreshObjectiveTarget(player);
        }
    }

    // The player moved. Completes the active ReachLocation goal (Type=1) of any in-progress quest
    // when the player is within the goal's radius (2D X/Z). Runs on every client position update
    // (~10-20 Hz), so it early-outs everything that isn't an active reach goal.
    public void OnPlayerMoved(Player player)
    {
        foreach (var (questId, quest, done, goal) in ActiveGoals(player))
        {
            if (goal.Type != QuestGoalType.ReachLocation || goal.ReachPosition.Length < 3)
                continue;

            var dx = player.Position.X - goal.ReachPosition[0];
            var dz = player.Position.Z - goal.ReachPosition[2];
            var radius = goal.ReachRadius > 0 ? goal.ReachRadius : 12f;
            if (dx * dx + dz * dz > radius * radius)
                continue;

            CompleteGoal(player, quest, done);
        }
    }

    // Loads a player's DbCharacterQuest row, applies the mutation, and saves - the shared shape behind
    // every quest progress write (collect count, goal progress, completion).
    private void UpdateCharacterQuest(Player player, int questId, Action<DbCharacterQuest> update)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var dbQuest = db.CharacterQuests.FirstOrDefault(x => x.QuestId == questId && x.CharacterId == player.CharacterId);
        if (dbQuest is null)
            return;

        update(dbQuest);
        db.SaveChanges();
    }

    // Persists the active Collect goal's in-progress count (DbCharacterQuest.GoalCount).
    private void PersistCollectCount(Player player, int questId, int count)
        => UpdateCharacterQuest(player, questId, q => q.GoalCount = count);

    // Re-sends this quest's collectible pickups to the player so any hidden in a prior attempt reappear and
    // are clickable again: AddNpc (re-adds the model; a no-op for one still showing) PLUS an NpcRelevance
    // entry - that relevance packet, not just AddNpc's IsInteractable flag, is what registers a pickup as
    // interactable client-side (this is how zone-entry wires them up). NB: no RemovePlayer first - a
    // remove+re-add of the same guid races and can leave the pickup gone.
    private void RespawnQuestCollectibles(Player player, int questId)
    {
        var relevance = new PlayerUpdatePacketNpcRelevance();

        foreach (var entry in _resourceManager.Quests.Collectibles)
        {
            if (entry.Value.QuestId != questId)
                continue;
            if (!player.Zone.TryGetNpc(entry.Key, out var npc))
                continue;

            // Re-showing every pickup: forget which ones were "collected" so the marker treats them all
            // as available again (matches the in-memory count reset that happens on re-accept/relog).
            player.CollectedPickups.Remove(entry.Key);

            player.SendTunneled(npc.GetAddNpcPacket());

            if (npc.CursorId != 0)
            {
                relevance.Entries.Add(new PlayerUpdatePacketNpcRelevance.Entry
                {
                    Guid = npc.Guid,
                    HasCursor = true,
                    CursorId = npc.CursorId,
                    Unknown2 = true
                });
            }
        }

        if (relevance.Entries.Count > 0)
            player.SendTunneled(relevance);
    }

    public void AcceptQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest) || !quest.IsOfferableFor(player.Quests))
            return;

        player.Quests[questId] = false;
        player.QuestGoalProgress.Remove(questId); // fresh accept starts on the first goal
        player.QuestCollectProgress.Remove(questId); // and with no collect progress
        player.ActiveQuestId = questId; // a freshly accepted quest becomes the tracked one
        player.LastQuestAcceptedAt = DateTime.UtcNow; // guards against a stray post-accept QuestAbandon

        using (var db = _dbContextFactory.CreateDbContext())
        {
            db.CharacterQuests.Add(new DbCharacterQuest
            {
                QuestId = questId,
                CharacterId = player.CharacterId,
                Completed = false
            });
            db.SaveChanges();
        }

        SendActiveState(player, quest);

        // Restore this quest's collectible pickups for the player: any collected in a PRIOR attempt were
        // hidden with RemovePlayer (which persists until relog), so without this a collect-then-abandon-then-
        // reaccept would leave fewer than RequiredCount pickups and the goal could never finish.
        RespawnQuestCollectibles(player, questId);

        RefreshQuestNotifications(player, quest);

        // Finalize the interaction so the offer camera doesn't stay frozen on the giver (sub-opcode 29
        // recomputes the camera + dispatches QuestStartHandler:DismissEndScreen).
        player.SendTunneled(new CommandPacketQuestDialogComplete());
    }

    public void CompleteQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest))
            return;

        if (player.Quests.TryGetValue(questId, out var done) && done)
            return; // already finalized

        player.Quests[questId] = true;
        player.QuestCollectProgress.Remove(questId);
        UpdateCharacterQuest(player, questId, q => q.Completed = true);

        player.SendTunneled(new QuestCompletePacket { QuestId = questId });

        // Bump the journal's lifetime "quests completed" counter (op49/12).
        player.SendTunneled(new CompletedQuestCountUpdatePacket
        {
            Count = player.Quests.Values.Count(done => done)
        });

        // Mark this quest complete in the storybook Adventurer's Journal (op209/2) so its sticker earns.
        SendJournalQuestStates(player);

        GrantReward(player, quest);

        // Clear the badges on both quest NPCs.
        RefreshQuestNotifications(player, quest);

        // The next quest in the chain becomes offerable automatically (IsOfferable checks the prereq);
        // refresh its giver's badge so the "!" appears without a relog if that NPC is already spawned.
        if (quest.NextQuestId != 0 && _resourceManager.Quests.TryGet(quest.NextQuestId, out var next))
            RefreshQuestNotification(player, next.GiverGuid);

        // Clear the completed quest's tracker arrow / mini-map indicator (or re-point at another active quest).
        RefreshObjectiveTarget(player);
    }

    public void AbandonQuest(Player player, int questId)
    {
        // Ignore a stray abandon fired in the moments right after accepting (the client has been seen
        // retransmitting it around the accept flow) - that would drop a just-taken quest.
        if ((DateTime.UtcNow - player.LastQuestAcceptedAt).TotalSeconds < 3)
            return;

        // Prefer the id the client sent; if it isn't a quest the player currently has active, fall back
        // to their single active quest (guards against the client sending an unexpected id).
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

        using (var db = _dbContextFactory.CreateDbContext())
        {
            var dbQuest = db.CharacterQuests.FirstOrDefault(x => x.QuestId == questId && x.CharacterId == player.CharacterId);
            if (dbQuest is not null)
            {
                db.CharacterQuests.Remove(dbQuest);
                db.SaveChanges();
            }
        }

        // Tell the client to remove the quest from the Hero's Journal, then restore the giver's "!".
        player.SendTunneled(new QuestAbandonedPacket { QuestId = questId });

        RefreshQuestNotifications(player, quest);

        // Remove the now-dangling tracker arrow / mini-map indicator (re-point at another active quest, or clear).
        RefreshObjectiveTarget(player);
    }

    public void SetActiveQuest(Player player, int questId)
    {
        if (!_resourceManager.Quests.TryGet(questId, out var quest))
            return;

        if (player.Quests.TryGetValue(questId, out var completed) && !completed)
        {
            player.ActiveQuestId = questId; // this is now the tracked quest for the arrow + "Take Me There"

            int done = player.QuestGoalProgress.TryGetValue(questId, out var progress) ? progress : 0;
            var goals = quest.EffectiveGoals;

            if (done < goals.Count)
                SendObjectiveActivated(player, questId, goals[done]);

            // Point the tracker/breadcrumb at the active goal's target.
            SendObjectiveForGoal(player, quest, done);
        }
    }

    public void RestoreJournal(Player player)
    {
        foreach (var (questId, completed) in player.Quests)
        {
            if (!completed && _resourceManager.Quests.TryGet(questId, out var quest))
                SendActiveState(player, quest);
        }

        // Seed the journal's lifetime "quests completed" counter (op49/12) from the DB-backed state.
        player.SendTunneled(new CompletedQuestCountUpdatePacket
        {
            Count = player.Quests.Values.Count(done => done)
        });

        // Seed the storybook Adventurer's Journal's completed-quest set (op209/2) so earned stickers
        // show as complete on login.
        SendJournalQuestStates(player);
    }

    // Pushes the storybook Adventurer's Journal quest-state map (op209/2 QuestUpdate). RE-verified
    // (FUN_00a44020): a quest id being PRESENT in this map marks it completed in the journal (the value
    // is only used for ordering), so we send every completed quest id. Sent on login + after each
    // completion. Harmless for quests that aren't journal stickers - the client just ignores unknown ids.
    private void SendJournalQuestStates(Player player)
    {
        var states = new Dictionary<int, int>();
        foreach (var (questId, completed) in player.Quests)
            if (completed)
                states[questId] = 1; // presence = completed; value is ordering only

        if (states.Count > 0)
            player.SendTunneled(new AdventurersJournalQuestUpdatePacket { QuestStates = states });
    }

    // Refreshes both the giver's and target's badge, plus any mutually-exclusive quest's badges
    // (ExcludesQuestIds) - accepting/completing/abandoning this quest can flip whether those are
    // offerable too, and their badge wouldn't otherwise catch up until some unrelated event.
    private void RefreshQuestNotifications(Player player, QuestDefinition quest)
    {
        RefreshQuestNotification(player, quest.GiverGuid);
        RefreshQuestNotification(player, quest.TargetGuid);

        foreach (var excludedId in quest.ExcludesQuestIds)
        {
            if (!_resourceManager.Quests.TryGet(excludedId, out var excludedQuest))
                continue;

            RefreshQuestNotification(player, excludedQuest.GiverGuid);
            RefreshQuestNotification(player, excludedQuest.TargetGuid);
        }
    }

    public void RefreshQuestNotification(Player player, ulong npcGuid)
    {
        if (npcGuid == 0 || !player.Zone.TryGetNpc(npcGuid, out var npc))
            return;

        var imageId = player.GetNotificationImageId(npc);

        // A plain AddNpc resend does NOT live-update an already-spawned NPC's world badge (confirmed
        // live) - remove the NPC and re-add it with the updated NotificationImageSetId instead.
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
            player.SendTunneled(new PlayerUpdatePacketRemoveNotifications { Guids = { npc.Guid } });
            return;
        }

        player.SendTunneled(new PlayerUpdatePacketAddNotifications
        {
            Notifications =
            {
                new NotificationInfo
                {
                    Guid = npc.Guid,
                    Combat = false,
                    ImageId = imageId,
                    NameId = npc.NameId,
                    SubTextId = npc.SubTextNameId,
                }
            }
        });
    }

    // Sends the quest offer popup (QuestInfoPacket) for the giver NPC.
    private void Offer(Player player, QuestDefinition quest)
    {
        player.SendTunneled(new QuestInfoPacket
        {
            QuestId = quest.QuestId,
            // TitleId drives the visible NPCText bubble and, on a stock client, also writes to the chat
            // log - none of the 4 unknown fields below toggle that off (Unknown7 = members-only gate,
            // Unknown10/11 = no visible effect, Unknown12 = accept-only/no decline button).
            TitleId = quest.GiverDialogueId,
            DescriptionId = quest.DescriptionId,
            // The collapsed details-box line: the quest name, retail-style ("Welcome to Seaside").
            HelperTextId = quest.TitleId,
            IconId = quest.IconId,
            Unknown6 = quest.ObjectiveDescriptionId, // offer "Goals" list
            Unknown7 = false,
            NpcGuid = quest.GiverGuid,
            Unknown10 = 0,
            Unknown11 = false,
            Unknown12 = false,
            RewardCoins = quest.RewardCoins,
            RewardExperience = quest.RewardExperience, // job XP shown in the reward preview
            RewardItems = BuildRewardItems(quest) // item icons in the "Show Details" reward preview
        });
    }

    // Resolves a quest's RewardItems def ids into reward-preview entries
    // (icon + name + count) by looking up each item's ClientItemDefinition. Shown as icons in the offer
    // and turn-in "Show Details" panels.
    private List<RewardBundleItem> BuildRewardItems(QuestDefinition quest)
    {
        var items = new List<RewardBundleItem>();
        foreach (var definitionId in quest.RewardItems)
        {
            if (_resourceManager.ClientItemDefinitions.TryGetValue(definitionId, out var itemDef))
            {
                items.Add(new RewardBundleItem
                {
                    IconId = itemDef.Icon.Id,
                    NameId = itemDef.NameId,
                    Count = 1
                });
            }
        }
        return items;
    }

    // Ticks off the goal at goalIndex: sends the objective checkmark, advances the
    // player's progress, then either activates+retargets the next goal or, when this was the last goal,
    // hands the quest in (reward + end screen). Goals complete in order.
    private void CompleteGoal(Player player, QuestDefinition quest, int goalIndex)
    {
        var goals = quest.EffectiveGoals;

        // The final goal ticks SILENTLY (checkmark, no "Goal Complete!" banner): the "Quest Completed!" banner
        // fires right after on turn-in, and two banners back-to-back make the second wait on the first's
        // animation. Intermediate goals still banner normally.
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

        // Persist progress so a relog mid-quest resumes on the right goal.
        UpdateCharacterQuest(player, quest.QuestId, q =>
        {
            q.GoalProgress = done;
            q.GoalCount = 0; // moving to the next goal - clear any collect count from the finished one
        });

        if (done >= goals.Count)
        {
            // Final goal done -> hand in (reward + "Quest Complete" end screen).
            TurnIn(player, quest);
            return;
        }

        // More goals to go: REVEAL the next goal's row now (progressive, retail-style — the helper
        // shows completed rows plus the CURRENT one, never the quest's whole future checklist),
        // then activate it and re-point the tracker/breadcrumb at its target.
        player.SendTunneled(new QuestObjectiveAddedPacket
        {
            QuestId = quest.QuestId,
            ObjectiveNameId = goals[done].NameId,
            ObjectiveDescriptionId = goals[done].NameId,
            ObjectiveField2 = goals[done].DescriptionId != 0 ? goals[done].DescriptionId : goals[done].NameId
        });
        SendObjectiveActivated(player, quest.QuestId, goals[done]);
        SendObjectiveForGoal(player, quest, done);

        // Mid-quest reply bubble - only TalkToNpc goals get one, since they complete AT an NPC. Other
        // goal types fire from field events with no NPC to camera-focus, and their DialogueId is just
        // the giver's mid-goal reminder line - popping it at the trigger moment would read wrong.
        var completedGoal = goals[goalIndex];
        if (completedGoal.DialogueId != 0 && completedGoal.Type == QuestGoalType.TalkToNpc)
        {
            var dialog = new CommandPacketShowDialog
            {
                DialogueTextId = completedGoal.DialogueId,
                NpcGuid = GoalTargetGuid(quest, goalIndex),
                CameraFocusParam = 1f,
            };

            dialog.Responses.Add(new CommandPacketShowDialog.Response
            {
                Id = 1,
                LabelTextId = YouGotItTextId, // "You got it!"
                Param1 = GreenCheckImageId,   // node+0x14 -> button icon = green checkmark (confirmed in-game)
                Param2 = GreenButtonImageSet, // node+0x18 -> button skin = "dialog green button" imageSet
            });
            player.SendTunneled(dialog);
        }
    }

    // Global.Text id for the generic "You got it!" response button.
    private const int YouGotItTextId = 103085;

    // Image id of ui_dialog_greencheck (Images.txt) - the response button's green check icon.
    private const int GreenCheckImageId = 300;

    // ImageSet id 17 = "dialog green button" (ImageSets.txt) - the green response-button skin.
    private const int GreenButtonImageSet = 17;

    // Shows the "Quest Complete" end screen; finalize happens on the Complete click. The completing
    // goal's checkmark is already sent by CompleteGoal before this is called.
    private void TurnIn(Player player, QuestDefinition quest)
    {
        // No QuestAdd re-send here: the end screen's bubble reads QuestEndPacket's own TitleId field
        // below, not QuestData at all, so nothing needs refreshing. Re-sending QuestAdd would APPEND a
        // duplicate journal row (the client never dedupes) that completion then can't fully clear -
        // the bug that left finished quests in the journal.
        player.SendTunneled(new QuestEndPacket
        {
            // Camera focus = the LAST goal's NPC (where hand-in happens). For single-goal quests this is
            // quest.TargetGuid; for multi-goal it's the final goal's target (e.g. back at the giver).
            NpcGuid = GoalTargetGuid(quest, quest.EffectiveGoals.Count - 1),
            QuestId = quest.QuestId,
            // With the ScriptsBase details-split applied, the end screen's speech bubble reads
            // SetNPCDialog(showEndText), and showEndText is fed by THIS packet's TitleId field (verified
            // in-game: the bubble showed whatever went here). So put the turn-in DIALOGUE here. The panel
            // title + "Show Details" description come from QuestData columns 1/2 (set by SendActiveState:
            // col1=TitleId title, col2=ObjectiveDescriptionId objective), independent of this packet.
            TitleId = quest.TurnInDialogueId, // -> showEndText -> speech bubble = the NPC's turn-in line
            DescriptionId = quest.TitleId,    // -> showEndId (not rendered as text); harmless
            RewardCoins = quest.RewardCoins,
            RewardExperience = quest.RewardExperience, // job XP shown in the reward preview
            RewardItems = BuildRewardItems(quest) // item icons in the "Show Details" reward preview
        });

        // Reward/completion is applied when the player clicks "Complete" (QuestEndReply invokes this).
        player.PendingQuestEndAction = () => CompleteQuest(player, quest.QuestId);
    }

    // HelperTextId (QuestData column 10) does double duty: a patched client reads it as the end-screen
    // bubble text, but a STOCK client reads the same column as the tracker widget's header WHILE the
    // quest is active (confirmed via disassembly). So it has to stay short here - pass
    // ObjectiveDescriptionId, not the long TurnInDialogueId. The turn-in bubble itself doesn't care what's
    // sent here either way - it reads QuestEndPacket's own TitleId instead (see TurnIn()).
    private static void SendQuestAdd(Player player, QuestDefinition quest, int helperTextId, float completedPercentage = 0f)
    {
        player.SendTunneled(new QuestAddPacket
        {
            QuestId = quest.QuestId,
            TitleId = quest.TitleId,
            // DescriptionId (client QuestData col 2) feeds BOTH the on-screen tracker's header line AND the
            // StoryBook journal's right-page description. Use the objective ("Introduce yourself to X in Y")
            // so the tracker header reads as the objective; the shorter sub-goal ("Talk to X") is the goal
            // row (QuestObjectiveAddedPacket, from the goal's NameId). They share this one client slot, so
            // the journal description shows the objective too rather than the longer flavour DescriptionId.
            DescriptionId = quest.ObjectiveDescriptionId,
            HelperTextId = helperTextId,
            MembersOnly = false,
            TimeStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ProfileId = 0,
            CompletedPercentage = completedPercentage,
            IconId = quest.IconId,
            SystemQuest = false
        });
    }

    // QuestAdd + objective packets that put the quest into the client's journal + tracker.
    private void SendActiveState(Player player, QuestDefinition quest)
    {
        int alreadyDone = player.QuestGoalProgress.TryGetValue(quest.QuestId, out var p) ? p : 0;
        SendQuestAdd(player, quest, quest.ObjectiveDescriptionId, (float)alreadyDone / quest.EffectiveGoals.Count);

        // PROGRESSIVE REVEAL: objective rows exist only for goals already completed plus the ACTIVE
        // one — the helper shows where you've been and what's next, never the quest's whole future
        // checklist (the retail shape: Tides of Change opens showing just "Talk to Mayor Gilly").
        // CompleteGoal adds each subsequent row at the moment it activates; this relog replay
        // rebuilds the same visible set.
        var goals = quest.EffectiveGoals;
        int done = player.QuestGoalProgress.TryGetValue(quest.QuestId, out var progress) ? progress : 0;
        int lastVisible = System.Math.Min(done, goals.Count - 1);

        for (int i = 0; i <= lastVisible; i++)
        {
            player.SendTunneled(new QuestObjectiveAddedPacket
            {
                QuestId = quest.QuestId,
                // Body int0 is the objective's IDENTITY (the client hashes rows by it - traced
                // FUN_00bab950: row+0xf0 = int0) AND its name text id; Activated/Complete find the row
                // by sending the same value as ObjectiveId. Goal NameIds must therefore be unique
                // within a quest. (A raw index here broke everything: id 0 rendered as
                // "<STRING 0 NOT FOUND>" and the Activated/Complete lookups missed, so checkmarks and
                // goal advance never showed client-side.)
                ObjectiveNameId = goals[i].NameId,
                // The tracker goal row renders from body int1 ("Talk to Shakey").
                ObjectiveDescriptionId = goals[i].NameId,
                // Body int2 = the journal "Objectives" sub-line ("Shakey should be hanging out in
                // front of the Wildwood Speedway...").
                ObjectiveField2 = goals[i].DescriptionId != 0 ? goals[i].DescriptionId : goals[i].NameId
            });
        }

        // Replay already-completed goals as ticked (restores checkmarks after relog).
        for (int i = 0; i < done && i < goals.Count; i++)
        {
            player.SendTunneled(new QuestObjectiveCompletePacket
            {
                QuestId = quest.QuestId,
                ObjectiveId = goals[i].NameId,
                Percent = 1f,
                Silent = true // relog replay -> tick the checkmark but don't re-banner old goals
            });
        }

        // Activate the current goal (the first not-yet-done one).
        if (done < goals.Count)
        {
            var activeGoal = goals[done];
            SendObjectiveActivated(player, quest.QuestId, activeGoal);

            // If it's a Collect goal with restored progress (relog mid-count), show the current count
            // so the tracker reads e.g. 3/8 instead of 0/8. Activated only sets the "required" half.
            if (activeGoal.Type == QuestGoalType.Collect
                && player.QuestCollectProgress.TryGetValue(quest.QuestId, out var collected) && collected > 0)
            {
                int req = activeGoal.RequiredCount > 0 ? activeGoal.RequiredCount : activeGoal.CollectSpawns.Count;
                player.SendTunneled(new QuestObjectiveUpdatePacket
                {
                    QuestId = quest.QuestId,
                    ObjectiveId = activeGoal.NameId,
                    CurrentCount = collected,
                    CompletedPercentage = req > 0 ? (float)collected / req : 0f
                });
            }
        }

        // Point the tracker + "Take Me There" breadcrumb at the active goal's target NPC.
        SendObjectiveForGoal(player, quest, done);
    }

    private static void SendObjectiveActivated(Player player, int questId, QuestGoal goal)
    {
        player.SendTunneled(new QuestObjectiveActivatedPacket
        {
            QuestId = questId,
            ObjectiveId = goal.NameId,
            RequiredCount = goal.RequiredCount,
            Unknown2 = false
        });
    }

    // The NPC guid the goal at goalIndex points at: the goal's own TargetGuid, or the
    // quest's turn-in TargetGuid when the goal doesn't override it (or when all goals are already done).
    private static ulong GoalTargetGuid(QuestDefinition quest, int goalIndex)
    {
        var goals = quest.EffectiveGoals;
        if (goalIndex >= 0 && goalIndex < goals.Count && goals[goalIndex].TargetGuid != 0)
            return goals[goalIndex].TargetGuid;
        return quest.TargetGuid;
    }

    // Player-aware objective target: the NPC the tracker arrow / "Take Me There" breadcrumb should point
    // at for the active goal.
    private ulong ResolveGoalTargetGuid(Player player, QuestDefinition quest, int goalIndex)
    {
        var goals = quest.EffectiveGoals;

        // A Collect goal has no fixed NPC: point at the NEAREST pickup this player hasn't taken yet, so
        // the marker/breadcrumb leads to the tools. Any pickup credits the goal (it's a counter), so
        // this is guidance only - the player can grab whichever they find first.
        if (goalIndex >= 0 && goalIndex < goals.Count
            && goals[goalIndex].Type == QuestGoalType.Collect)
        {
            var nearest = NearestUncollectedPickup(player, quest.QuestId, goalIndex);
            if (nearest is not null)
                return nearest.Guid;
        }

        return GoalTargetGuid(quest, goalIndex);
    }

    // Nearest Collect pickup for (questId, goalIndex) that this player hasn't gathered yet, or null when
    // none remain in this zone. Pickups are the collectible NPCs spawned from the goal's CollectSpawns.
    private Npc? NearestUncollectedPickup(Player player, int questId, int goalIndex)
    {
        Npc? nearest = null;
        var best = float.MaxValue;
        foreach (var (guid, loc) in _resourceManager.Quests.Collectibles)
        {
            if (loc.QuestId != questId || loc.GoalIndex != goalIndex)
                continue;
            if (player.CollectedPickups.Contains(guid))
                continue;
            if (!player.Zone.TryGetNpc(guid, out var pickup))
                continue;
            var dx = pickup.Position.X - player.Position.X;
            var dz = pickup.Position.Z - player.Position.Z;
            var d2 = dx * dx + dz * dz;
            if (d2 < best)
            {
                best = d2;
                nearest = pickup;
            }
        }
        return nearest;
    }

    // Goal-aware objective indicator: ReachLocation pins its destination; every other goal type points
    // at its target NPC.
    private void SendObjectiveForGoal(Player player, QuestDefinition quest, int goalIndex)
    {
        var goals = quest.EffectiveGoals;

        // ReachLocation: pin the destination itself (Guid 0 - a place, not an entity). Label = the
        // goal row's text ("Take a look at the view").
        if (goalIndex >= 0 && goalIndex < goals.Count
            && goals[goalIndex].Type == QuestGoalType.ReachLocation
            && goals[goalIndex].ReachPosition.Length >= 3)
        {
            var rp = goals[goalIndex].ReachPosition;
            var reachPos = new Vector4(rp[0], rp[1], rp[2], 1f);
            var reachZoneId = player.Zone is StartingZone reachZone
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

    // Sends the ObjectiveTargetUpdatePacket that drives the tracker arrow, mini-map indicator and the
    // "Take Me There" green breadcrumb trail. Target is the given NPC guid (the active goal's NPC); if it
    // isn't spawned in the player's current zone we send nothing (no destination to point at).
    private void SendObjectiveTarget(Player player, ulong targetGuid)
    {
        if (targetGuid == 0 || !player.Zone.TryGetNpc(targetGuid, out var target))
            return;

        var pos = target.Position;
        var zoneAreaId = player.Zone is StartingZone startingZone
            ? startingZone.GetZoneAreaId(pos)
            : player.Zone.Id;

        player.SendTunneled(new ObjectiveTargetUpdatePacket
        {
            Active = true,
            LocationX = pos.X,
            LocationZ = pos.Z,
            ZoneId = zoneAreaId,
            Guid = targetGuid,
            // Display name shown on the tracker/mini-map indicator; the client resolves this id to the
            // label (0/invalid renders the "Default Housing NPC" fallback).
            NameId = target.NameId,
            PositionX = pos.X,
            PositionY = pos.Y,
            PositionZ = pos.Z,
            PositionW = 1f
        });
    }

    // Re-points the objective tracker/mini-map indicator at a still-active quest whose target NPC is
    // present, or clears it entirely (Active=false) when no trackable quest remains. Call after a quest
    // leaves the active set (abandon/complete) so a dangling indicator doesn't stay on screen.
    public void RefreshObjectiveTarget(Player player)
    {
        if (TryGetTrackedGoal(player, out var quest, out var goalIndex))
            SendObjectiveForGoal(player, quest, goalIndex);
        else
            player.SendTunneled(new ObjectiveTargetUpdatePacket { Active = false });
    }

    public bool TryGetActiveObjectiveTarget(Player player, out Vector3 targetPosition)
    {
        if (TryGetTrackedGoal(player, out var quest, out var goalIndex))
        {
            var goals = quest.EffectiveGoals;

            // Once every goal is done the index sits one past the end (the quest is waiting to be handed
            // in), so only look at the goal itself while it's still in range.
            var onGoal = goalIndex >= 0 && goalIndex < goals.Count;

            // Reach goal: walk to the destination itself.
            if (onGoal && goals[goalIndex].Type == QuestGoalType.ReachLocation
                && goals[goalIndex].ReachPosition.Length >= 3)
            {
                var rp = goals[goalIndex].ReachPosition;
                targetPosition = new Vector3(rp[0], rp[1], rp[2]);
                return true;
            }

            var guid = ResolveGoalTargetGuid(player, quest, goalIndex);
            if (guid != 0 && player.Zone.TryGetNpc(guid, out var target))
            {
                targetPosition = new Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                return true;
            }
        }

        targetPosition = default;
        return false;
    }

    // The quest + active goal the tracker/mini-map/Take Me There should follow: the player's selected
    // ActiveQuestId when it's still active and trackable in this zone; otherwise the first active quest
    // with a trackable goal. False when nothing is trackable.
    private bool TryGetTrackedGoal(Player player, out QuestDefinition quest, out int goalIndex)
    {
        // Prefer the quest the player actually has selected - the whole point of "make active" is that the
        // arrow and Take Me There follow IT, not whatever quest happens to be first in storage order.
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

    // The active goal of questId when it can be tracked from the player's current zone: every goal type
    // needs its resolved target NPC spawned here (Reach goals are always trackable - see below).
    private bool TryGetTrackableGoal(Player player, int questId, out QuestDefinition quest, out int goalIndex)
    {
        quest = null!;
        goalIndex = -1;
        if (!_resourceManager.Quests.TryGet(questId, out var q))
            return false;

        int done = player.QuestGoalProgress.TryGetValue(questId, out var progress) ? progress : 0;
        var goals = q.EffectiveGoals;

        // Reach goals are always trackable — the destination is a fixed world position.
        if (done >= 0 && done < goals.Count
            && goals[done].Type == QuestGoalType.ReachLocation
            && goals[done].ReachPosition.Length >= 3)
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

        // Job/profile XP - grant to the active job (updates the job's level bar).
        var experience = quest.RewardExperience;
        if (experience > 0)
            player.AwardXp(experience);

        // Reward-earned celebration (coins + XP fly-in with sound).
        if (coins > 0 || experience > 0)
            player.SendTunneled(new QuestRewardBundlePacket { Coins = coins, Xp = experience });

        // Item rewards - defined per quest in Resources/Quests.json ("RewardItems": [id, ...]).
        foreach (var itemDefinitionId in quest.RewardItems)
        {
            GrantItem(player, itemDefinitionId);

            // "You earned an item" celebration (opcode 50/2): shows the item icon + "received 1".
            player.SendTunneled(new RewardNonBundledItemPacket { ItemDefinitionId = itemDefinitionId, Quantity = 1 });
        }
    }

    // Grants one of definitionId to the player: stacks it in the DB (by definition +
    // tint), mirrors it into the in-memory inventory, and tells the client (ItemAdd for a new item, or
    // ItemUpdate for an incremented stack). Mirrors the coin-store grant path.
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
