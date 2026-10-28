using System;
using System.Collections.Generic;
using System.Threading;

using Sanctuary.Game.Entities;
using Sanctuary.Game.MiniGames;
using Sanctuary.Game.MiniGames.CombatEncounters;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class MiniGameHelper
{
    private static readonly int[] StoreAdvertisementBundleIds = [2194, 2919, 2920, 2921, 3752];

    private enum EncounterState
    {
        Created = 2,
        Loading = 3,
        DetailsReady = 4,
        ZoneReady = 5,
        Running = 6
    }

    private enum EncounterDetailsPhase
    {
        Details = 2,
        Running = 4
    }

    private enum ObjectiveStatus
    {
        Inactive,
        Active
    }

    private const int NoMiniGameId = -1;
    private const int NoEncounterId = 0;
    private const int NoEncounterDefinitionId = 0;
    private const ulong NoParticipantGuid = 0;
    private const int ParticipantUnknown3 = 1;
    private const int MiniGameUnknown14 = 1;

    private const int DefaultGameTimeOverride = -1;
    private const float DefaultObjectiveMultiplier = 1f;
    private const int DefaultQuickCompletionTime = 3600;
    private const int DefaultNpcKOValue = 1;

    private static int _encounterId;

    public static MiniGameUiSession ShowDetails(Player player, MiniGameUiDefinition definition)
    {
        var miniGameUiSession = new MiniGameUiSession(Interlocked.Increment(ref _encounterId), definition, player);

        player.MiniGameUiSession = miniGameUiSession;

        SendEncounterState(miniGameUiSession, EncounterState.Created);
        SendEncounterState(miniGameUiSession, EncounterState.Loading);
        SendEncounterState(miniGameUiSession, EncounterState.DetailsReady);

        SendEncounterDetails(miniGameUiSession, EncounterDetailsPhase.Details);

        if (!CombatEncounterRegistry.TryGet(definition.GameId, out _))
            return miniGameUiSession;

        player.SendTunneled(new EncounterZoneIsReadyPacket
        {
            Unknown = miniGameUiSession.EncounterId,
            Unknown2 = definition.EncounterDefinitionId
        });

        SendEncounterState(miniGameUiSession, EncounterState.ZoneReady);

        return miniGameUiSession;
    }

    public static bool TryShowDetails(Player player, MiniGameUiDefinition definition)
    {
        if (player.MiniGameUiSession is { State: not MiniGameUiState.Details })
            return false;

        if (player.MiniGameUiSession is not null)
            Close(player, player.MiniGameUiSession);

        ShowDetails(player, definition);

        return true;
    }

    public static void RefreshDetails(Player player, MiniGameUiSession miniGameUiSession)
    {
        if (miniGameUiSession.State != MiniGameUiState.Details)
            return;

        SendEncounterDetails(miniGameUiSession, EncounterDetailsPhase.Details);
    }

    public static void Start(Player player, MiniGameUiSession miniGameUiSession)
    {
        miniGameUiSession.State = MiniGameUiState.Running;

        SendEncounterState(miniGameUiSession, EncounterState.Running);

        foreach (var miniGameUiObjective in miniGameUiSession.Definition.Objectives)
        {
            miniGameUiSession.ObjectiveProgress[miniGameUiObjective.Id] = default;

            player.SendTunneled(new ObjectiveActivatePacket
            {
                ObjectiveId = miniGameUiObjective.Id,
                Unknown = miniGameUiObjective.Total
            });

            if (!miniGameUiObjective.ShowTask)
                continue;

            player.SendTunneled(new TaskAddPacket
            {
                TaskId = miniGameUiObjective.Id,
                Unknown2 = miniGameUiObjective.NameId
            });
        }

        SendEncounterDetails(miniGameUiSession, EncounterDetailsPhase.Running);

        player.SendTunneled(new EncounterPacketPlayerEnter
        {
            Unknown = miniGameUiSession.EncounterId,
            Unknown2 = miniGameUiSession.Definition.EncounterDefinitionId,
            Unknown3 = NoParticipantGuid
        });

        player.SendTunneled(new EncounterPacketPlayerEnter
        {
            Unknown = miniGameUiSession.EncounterId,
            Unknown2 = miniGameUiSession.Definition.EncounterDefinitionId,
            Unknown3 = player.Guid
        });

        player.SendTunneled(miniGameUiSession.Definition.EncounterDataCommon);
    }

    public static void UpdateObjective(Player player, MiniGameUiSession miniGameUiSession, MiniGameUiObjective miniGameUiObjective, int progress)
    {
        miniGameUiSession.ObjectiveProgress[miniGameUiObjective.Id] = progress;

        SendEncounterDetails(miniGameUiSession, EncounterDetailsPhase.Running);

        if (progress < miniGameUiObjective.Total)
            return;

        player.SendTunneled(new ObjectiveCompletePacket
        {
            ObjectiveId = miniGameUiObjective.Id,
            Unknown2 = miniGameUiObjective.CompleteValue
        });

        if (!miniGameUiObjective.ShowTask)
            return;

        player.SendTunneled(new TaskCompletePacket
        {
            TaskId = miniGameUiObjective.Id
        });
    }

    public static void UpdateKnockOuts(Player player, MiniGameUiSession miniGameUiSession, int knockOuts, int maxKnockOuts)
    {
        miniGameUiSession.KnockOuts = knockOuts;
        miniGameUiSession.MaxKnockOuts = maxKnockOuts;

        player.SendTunneled(new MiniGameKnockOutPacket
        {
            StateId = NoMiniGameId,
            GameId = NoMiniGameId,
            GroupId = NoMiniGameId,
            Unknown = knockOuts,
            Unknown2 = maxKnockOuts
        });
    }

    public static void AwardPopup(Player player, RewardBundlePacket rewardBundlePacket)
    {
        player.SendTunneled(rewardBundlePacket);
    }

    public static void ShowEndScreen(Player player, MiniGameUiSession miniGameUiSession, List<MiniGameScoreInfo> scores, int earnedStars, int timeRemaining, RewardBundleInfo rewardBundle, RewardBundleInfo memberRewardBundle, RewardBundleInfo? lootWheelRewardBundle = null)
    {
        miniGameUiSession.State = MiniGameUiState.Ended;

        if (lootWheelRewardBundle is not null)
        {
            player.SendTunneled(new MiniGameLootWheelSetItemToLandOnPacket
            {
                StateId = miniGameUiSession.EncounterId,
                GameId = NoMiniGameId,
                GroupId = NoMiniGameId,
                RewardBundle = lootWheelRewardBundle
            });
        }

        var miniGameGameEndScorePacket = new MiniGameGameEndScorePacket
        {
            StateId = NoMiniGameId,
            GameId = NoMiniGameId,
            GroupId = NoMiniGameId
        };

        miniGameGameEndScorePacket.Scores.AddRange(scores);

        player.SendTunneled(miniGameGameEndScorePacket);

        player.SendTunneled(new EncounterPacketEncounterComplete
        {
            Unknown = NoEncounterId,
            Unknown2 = NoEncounterDefinitionId,
            Unknown3 = true,
            EarnedStars = earnedStars,
            TimeRemaining = timeRemaining,
            RewardBundle = rewardBundle,
            MemberRewardBundle = memberRewardBundle
        });
    }

    public static void Close(Player player, MiniGameUiSession miniGameUiSession)
    {
        player.MiniGameUiSession = null;

        SendTerminate(player, miniGameUiSession);

        SendDefaultEncounterDataCommon(player);
    }

    public static void Leave(Player player, MiniGameUiSession miniGameUiSession)
    {
        miniGameUiSession.State = MiniGameUiState.Leaving;

        SendTerminate(player, miniGameUiSession);
    }

    public static void OnClientIsReady(Player player)
    {
        var miniGameUiSession = player.MiniGameUiSession;

        if (miniGameUiSession is null)
            return;

        if (miniGameUiSession.State == MiniGameUiState.Entering)
        {
            Start(player, miniGameUiSession);
            return;
        }

        if (miniGameUiSession.State != MiniGameUiState.Leaving)
            return;

        player.MiniGameUiSession = null;

        SendDefaultEncounterDataCommon(player);
    }

    private static void SendTerminate(Player player, MiniGameUiSession miniGameUiSession)
    {
        player.SendTunneled(new EncounterParticipantTerminateEncounterPacket
        {
            Unknown = miniGameUiSession.EncounterId,
            Unknown2 = miniGameUiSession.Definition.EncounterDefinitionId
        });
    }

    private static void SendDefaultEncounterDataCommon(Player player)
    {
        player.SendTunneled(new EncounterDataCommon
        {
            GameTimeOverride = DefaultGameTimeOverride,
            ObjectiveMultiplier = DefaultObjectiveMultiplier,
            QuickCompletionTime = DefaultQuickCompletionTime,
            NpcKOValue = DefaultNpcKOValue
        });
    }

    private static void SendEncounterState(MiniGameUiSession miniGameUiSession, EncounterState encounterState)
    {
        miniGameUiSession.Player.SendTunneled(new EncounterStatePacket
        {
            Unknown = miniGameUiSession.EncounterId,
            Unknown2 = miniGameUiSession.Definition.EncounterDefinitionId,
            State = (int)encounterState
        });
    }

    private static void SendEncounterDetails(MiniGameUiSession miniGameUiSession, EncounterDetailsPhase encounterDetailsPhase)
    {
        var player = miniGameUiSession.Player;
        var definition = miniGameUiSession.Definition;
        var isRunning = encounterDetailsPhase == EncounterDetailsPhase.Running;

        var encounterDetailsResponsePacket = new EncounterDetailsResponsePacket
        {
            Unknown = miniGameUiSession.EncounterId,
            Unknown2 = definition.EncounterDefinitionId,
            Unknown3 = isRunning,
            Unknown4 = isRunning ? default : definition.GameId
        };

        if (!isRunning)
            encounterDetailsResponsePacket.Unknown5.AddRange(StoreAdvertisementBundleIds);

        var details = encounterDetailsResponsePacket.Details;

        details.GameId = miniGameUiSession.EncounterId;
        details.Unknown2 = (int)encounterDetailsPhase;
        details.Type = definition.DetailsType;
        details.Encounter = true;
        details.Unknown3 = definition.DetailsUnknown3;
        details.DurationMs = definition.DurationMs;
        details.RespawnTime = definition.RespawnTime;
        details.Unknown5 = true;

        var encounterParticipantGroupInfo = new EncounterParticipantGroupInfo();

        encounterParticipantGroupInfo.Participants.Add(new(default, new EncounterParticipantInfo
        {
            Unknown = player.Guid,
            Unknown3 = ParticipantUnknown3,
            Unknown6 = true,
            Unknown7 = isRunning,
            Unknown10 = player.Position,
            Unknown11 = player.Name.FullName,
            Unknown14 = definition.EncounterDefinitionId
        }));

        details.ParticipantGroups.Add(encounterParticipantGroupInfo);

        var miniGame = details.MiniGame;

        miniGame.NameId = definition.NameId;
        miniGame.IconId = definition.IconId;
        miniGame.DescriptionId = definition.DescriptionId;
        miniGame.Difficulty = definition.Difficulty;
        miniGame.ProfileType = definition.ProfileType;
        miniGame.Type = definition.Type;
        miniGame.MembersOnly = definition.MembersOnly;
        miniGame.RewardBundle = definition.RewardBundle;
        miniGame.MemberRewardBundle = definition.MemberRewardBundle;
        miniGame.PreviewRewardBundle = definition.ProfilePreviewRewardBundles.GetValueOrDefault(player.ActiveProfileId, definition.PreviewRewardBundle);
        miniGame.ShowStarCounter = definition.ShowStarCounter;
        miniGame.ShowStatusIcon = definition.ShowStatusIcon;
        miniGame.ShowActionBar = definition.ShowActionBar;
        miniGame.Unknown11 = definition.StartupInCombat;
        miniGame.ShowEndDialog = definition.ShowEndDialog;
        miniGame.Unknown13 = string.Empty;
        miniGame.Unknown14 = MiniGameUnknown14;
        miniGame.Unknown15 = true;
        miniGame.PreselectedGameId = definition.PreselectedGameId;
        miniGame.Unknown20 = isRunning ? definition.GameId : miniGameUiSession.EncounterId;

        foreach (var miniGameUiObjective in definition.Objectives)
        {
            miniGameUiSession.ObjectiveProgress.TryGetValue(miniGameUiObjective.Id, out var progress);

            miniGame.Objectives.Add(new AchievementObjectiveInfo
            {
                Id = miniGameUiObjective.Id,
                NameId = miniGameUiObjective.NameId,
                DescriptionId = miniGameUiObjective.DescriptionId,
                RewardBundle = miniGameUiObjective.RewardBundle,
                Status = (int)(isRunning ? ObjectiveStatus.Active : ObjectiveStatus.Inactive),
                Count = progress,
                Total = isRunning ? miniGameUiObjective.Total : default,
                Unknown8 = Convert.ToInt32(miniGameUiObjective.IsFailCondition)
            });
        }

        player.SendTunneled(encounterDetailsResponsePacket);
    }
}
