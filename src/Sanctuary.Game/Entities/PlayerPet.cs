using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Pathfinding;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Entities;

public sealed class PlayerPet
{
    private readonly Player _player;
    private readonly IResourceManager _resourceManager;

    public Npc? Pet { get; private set; }
    private int _summonedPetId;
    private bool _petActionActive;
    private bool _petGroomingActive;
    private int _petSpeedBuffEffectId;
    private long _petSpeedBuffRefreshAt;
    private readonly Queue<Vector3> _petNavMeshWaypoints = new();
    private PetState _petState = new();
    private readonly ConcurrentQueue<Action> _queuedActions = new();

    private sealed class PetState
    {
        public Vector3 FollowGoal;
        public Vector3 LastPosition;
        public Vector3 OwnerLastPosition;
        public Vector3 OwnerTickPosition;
        public readonly List<Vector3> OwnerGroundedPositions = [];
        public bool OwnerTeleported;
        public float ChaseClosestDistance;
        public long ChaseProgressAt;
        public bool CatchingUp;
        public float FollowSide;
        public bool OwnerMoving;
        public long OwnerMovedAt;
        public long FaceOwnerAt;
        public long RoamAt;
        public bool RoamFirstHold;
        public long IdleAnimationEndAt;
        public long StandAt;
        public int LastIdleAnimationId;
        public long StayUntil;
        public PetRestPostureDefinition? RestPosture;
        public long RestLoopAt;
        public long RestStandUntil;
        public long IdleAnimationAt;
        public long NoticeAt;
        public long NoticeLookUntil;
        public Vector3? InvestigateGoal;
        public long InvestigateGiveUpAt;
        public bool UiModeActive;
        public bool InWater;
        public long ShakeEndAt;
        public long ShakeAnimationAt;
        public int ShakeDurationMs;
        public readonly HashSet<ulong> NoticedCollectionNodes = [];
        public long TreasureAt;
        public bool TreasurePending;
        public long TreasureDigAt;
        public long TreasureChestAt;
        public long TreasureDigEndAt;
        public Vector3 TreasureDirection;
        public int TreasureSniffLegsLeft;
        public float PendingPlay;
        public long CareUpdatedAt;
        public long ThoughtBubbleAt;
        public float LowestNeed;
    }

    private const int PetDryOffAnimationGroupId = 5505;
    private const int PetLoopForever = -1;
    private const int PetLoopOnce = 1;
    private const int PetEnjoyAnimationGroupId = 5506;
    private const int PetFeedAnimationGroupId = 5507;
    private const int PetFoodBowlSlot = 9;
    private const int PetFeedDurationMs = 6000;
    private const int PetFeedHungerDelayMs = 900;
    private const int PetGroomDurationSeconds = 25;
    private const float PetGroomHygienePerMovement = 1f / 65536f;
    private const float PetDefaultScale = 1f;
    private const int PetNpcDisposition = 2;
    private const float PetWaypointTolerance = 0.5f;
    private const float PetFollowBehindDistance = 2f;
    private const float PetFollowSideDistance = 1.5f;
    private const float PetLeftSide = -1f;
    private const float PetRightSide = 1f;
    private const float PetLeftSideChance = 0.5f;
    private const byte OwnerJumpingState = 0x02;
    private const byte OwnerFallingState = 0x04;
    private const byte OwnerGlidingState = 0x20;
    private const float PetGroundedPositionSpacing = 0.5f;
    private const int PetGroundedPositionCount = 20;
    private const float PetTakeoffBackDistance = 3f;
    private const float PetStopDistance = 2.3f;
    private const float PetStopMinAngle = MathF.PI / 6f;
    private const float PetStopMaxAngle = MathF.PI * 7f / 18f;
    private const int PetRoamFirstMinDelayMs = 3800;
    private const int PetRoamFirstMaxDelayMs = 5300;
    private const int PetRoamMinDelayMs = 2000;
    private const int PetRoamMaxDelayMs = 4000;
    private const float PetRoamMinDistance = 1.5f;
    private const float PetRoamMaxDistance = 4.75f;
    private const float PetRoamMinMoveDistance = 1.5f;
    private const float PetRoamWalkSpeed = 2.5f;
    private const int PetRoamPickAttempts = 8;
    private const float PetCatchUpMaxSpeedMultiplier = 2f;
    private const float PetCatchUpGain = 0.5f;
    private const float PetCatchUpStartDistance = 10f;
    private const float PetCatchUpStopDistance = 2f;
    private const float PetPathfindDistance = 15f;
    private const float PetPathfindDistanceSquared = PetPathfindDistance * PetPathfindDistance;
    private const float PetPathfindMaxNodeDistance = 15f;
    private const float PetPathfindMaxNodeDistanceSquared = PetPathfindMaxNodeDistance * PetPathfindMaxNodeDistance;
    private const int PetOwnerStoppedDelayMs = 750;
    private const float PetMovedDistanceSquared = 0.0001f;
    private const float PetFacingMinDistanceSquared = 0.01f;
    private const int PetIdleAnimationMinDelayMs = 30000;
    private const int PetIdleAnimationMaxDelayMs = 60000;
    private const float PetBathroomChance = 0.1f;
    private const float PetStayChance = 1f / 3f;
    private const int PetStayMinMs = 15000;
    private const int PetStayMaxMs = 40000;
    private const float PetRestPostureChance = 0.5f;
    private const int PetStandPlayfulAnimationGroupId = 5204;
    private const int PetStandPlayfulDurationMs = 4000;
    private const int PetStandCowerAnimationGroupId = 5203;
    private const int PetStandCowerDurationMs = 2000;
    private const int PetStandHyperAnimationGroupId = 5209;
    private const int PetStandHyperDurationMs = 3500;
    private const int PetStandChaseTailAnimationGroupId = 5210;
    private const int PetStandChaseTailDurationMs = 4500;
    private const int PetStandExcitedJumpAnimationGroupId = 5212;
    private const int PetStandExcitedJumpDurationMs = 1300;
    private const int PetFidgetYawnAnimationGroupId = 5252;
    private const int PetFidgetYawnDurationMs = 5000;
    private const int PetFidgetLookAnimationGroupId = 5258;
    private const int PetFidgetLookDurationMs = 5333;
    private const int PetLieSleepAnimationGroupId = 5403;
    private const int PetLieSleepDurationMs = 4000;
    private const int PetLieLazyAnimationGroupId = 5405;
    private const int PetLieLazyDurationMs = 6000;
    private const int PetPlayfulAnimationGroupId = 5513;
    private const int PetPlayfulDurationMs = 4000;
    private const int PetLookAtOwnerAnimationGroupId = 5527;
    private const int PetLookAtOwnerDurationMs = 2667;
    private const int PetCowerAnimationGroupId = 5503;
    private const int PetCowerDurationMs = 2000;
    private const int PetShyAnimationGroupId = 5521;
    private const int PetShyDurationMs = 7792;
    private const int PetVocalizePlayfulAnimationGroupId = 5522;
    private const int PetVocalizePlayfulDurationMs = 2667;
    private const int PetFaceOwnerMinDelayMs = 0;
    private const int PetFaceOwnerMaxDelayMs = 300;
    private const long PetFaceOwnerDone = -1;
    private const float PetFollowWarpDistance = 100f;
    private const float OwnerTeleportDistance = 30f;
    private const int PetCannotCatchUpMs = 5000;
    private const float PetChaseProgressDistance = 0.5f;
    private const float PetWarpSideDistance = 1.2f;
    private const float PetSummonSideDistance = 2f;
    private const float PetWarpHeight = 6f;
    private const float PetMaxStepDownHeight = 3f;
    private const float PetMaxStepUpHeight = 1f;
    private const float PetStopArrivedDistanceSquared = 0.25f;
    private const int PetStopSettleMs = 500;
    private const int PetDogShakeDurationMs = 1200;
    private const int PetCatShakeDurationMs = 1400;
    private const int PetGroomDryOffDurationMs = PetCatShakeDurationMs;
    private const int PetStandNowMs = 0;
    private const int PetDanceAnimationGroupId = 3201;
    private const int PetDanceDurationMs = 4000;
    private const int PetDanceJoinMaxDelayMs = 3000;
    private const int PetDanceGapMinMs = 500;
    private const int PetDanceGapMaxMs = 2000;
    private const int PetStandAnimationGroupId = 5201;
    private const float PetCollectionNoticeRange = 10f;
    private const int PetSniffAnimationGroupId = 5520;
    private const int SnowhillPointOfInterestId = 3;
    private const int ShroudedGladePointOfInterestId = 8;
    private const int WugachugPointOfInterestId = 12;
    private const int CobblestoneVillagePointOfInterestId = 7;
    private const int BriarwoodPointOfInterestId = 4;
    private const int BriarheartPointOfInterestId = 10;
    private const int BlacksporePointOfInterestId = 16;
    private static readonly int[] PetTreasurePointOfInterestIds =
    [
        SnowhillPointOfInterestId,
        ShroudedGladePointOfInterestId,
        WugachugPointOfInterestId,
        CobblestoneVillagePointOfInterestId,
        BriarwoodPointOfInterestId,
        BriarheartPointOfInterestId,
        BlacksporePointOfInterestId
    ];
    private const string PetTreasureZoneName = "FabledRealms";
    private const float PetTreasureSpotRange = 5f;
    private const float PetTreasureSpotRangeSquared = PetTreasureSpotRange * PetTreasureSpotRange;
    private const float PetTreasureMinMood = 25f;
    private const int PetTreasureMinDelayMs = 15000;
    private const int PetTreasureMaxDelayMs = 50000;
    private const int PetTreasureSniffLegs = 3;
    private const float PetTreasureSniffMinDistance = 2f;
    private const float PetTreasureSniffMaxDistance = 3.5f;
    private const float PetTreasureSniffMaxTurn = MathF.PI / 3f;
    private const float PetTreasureSniffSpeed = 1.5f;
    private const int PetSniffWalkAnimationGroupId = 5602;
    private const int PetWalkAnimationGroupId = 2;
    private const int PetDiggingAnimationGroupId = 5525;
    private const int PetDigDurationMs = 4000;
    private const int PetTreasureChestDigDelayMs = 3670;
    private static readonly TimeSpan PetTreasureCooldown = TimeSpan.FromHours(8);
    private const int PetNoticeMinDelayMs = 45000;
    private const int PetNoticeMaxDelayMs = 90000;
    private const float PetNoticeRange = 10f;
    private const int PetNoticeLookMs = 4000;
    private const float PetInvestigateStopDistance = 1.5f;
    private const float PetInvestigateMaxOwnerDistance = 20f;
    private const int PetInvestigateTimeoutMs = 8000;
    private const int PetTreasureChestModelId = 151;
    private const float PetTreasureChestDistance = 2f;
    private const int PetTreasureChestSpawnAnimationId = 1404;
    private const int PetTreasureChestStandAnimationId = 1;
    private const int PetTreasureChestInteractRange = 5;
    private const byte PetTreasureChestCursorId = 18;
    private const int PetTreasureChestDigUpCompositeEffectId = 15984;
    private const int PetTreasureChestLifetimeMs = 60000;
    private const int OwnerTrailFirstSearchedFromEnd = 2;

    private const int PetTreasureChestCommonWeight = 80;
    private const int PetTreasureChestRareWeight = 18;
    private const int PetTreasureChestEliteWeight = 2;
    private const int PetTreasureChestCommonNameId = 140227;
    private const int PetTreasureChestRareNameId = 140234;
    private const int PetTreasureChestEliteNameId = 140235;
    private const int PetTreasureChestCommonOpenCompositeEffectId = 5595;
    private const int PetTreasureChestRareOpenCompositeEffectId = 5596;
    private const int PetTreasureChestEliteOpenCompositeEffectId = 5597;

    private static readonly (int Weight, int NameId, string TextureAlias, string RewardTableKey, string CoinRewardTableKey, int OpenCompositeEffectId)[] PetTreasureChestTiers =
    [
        (PetTreasureChestCommonWeight, PetTreasureChestCommonNameId, "Default1", "pet-treasure-common", "pet-treasure-common-coins", PetTreasureChestCommonOpenCompositeEffectId),
        (PetTreasureChestRareWeight, PetTreasureChestRareNameId, "Default2", "pet-treasure-rare", "pet-treasure-rare-coins", PetTreasureChestRareOpenCompositeEffectId),
        (PetTreasureChestEliteWeight, PetTreasureChestEliteNameId, "Default3", "pet-treasure-elite", "pet-treasure-elite-coins", PetTreasureChestEliteOpenCompositeEffectId)
    ];
    private static readonly int PetTreasureChestTotalWeight = PetTreasureChestTiers.Sum(tier => tier.Weight);

    private static readonly Dictionary<PetTrait, (int AnimationGroupId, int DurationMs)[]> PetTraitIdleAnimations = new()
    {
        [PetTrait.Friendly] = [(PetPlayfulAnimationGroupId, PetPlayfulDurationMs), (PetStandPlayfulAnimationGroupId, PetStandPlayfulDurationMs), (PetLookAtOwnerAnimationGroupId, PetLookAtOwnerDurationMs)],
        [PetTrait.Lazy] = [(PetFidgetYawnAnimationGroupId, PetFidgetYawnDurationMs), (PetLieLazyAnimationGroupId, PetLieLazyDurationMs), (PetLieSleepAnimationGroupId, PetLieSleepDurationMs)],
        [PetTrait.Nervous] = [(PetCowerAnimationGroupId, PetCowerDurationMs), (PetStandCowerAnimationGroupId, PetStandCowerDurationMs), (PetFidgetLookAnimationGroupId, PetFidgetLookDurationMs), (PetShyAnimationGroupId, PetShyDurationMs)],
        [PetTrait.Excited] = [(PetStandChaseTailAnimationGroupId, PetStandChaseTailDurationMs), (PetStandExcitedJumpAnimationGroupId, PetStandExcitedJumpDurationMs), (PetStandHyperAnimationGroupId, PetStandHyperDurationMs), (PetVocalizePlayfulAnimationGroupId, PetVocalizePlayfulDurationMs)]
    };
    private const int PetSpeedBuffDefinitionId = 57918;
    private const int PetSpeedBuffCompositeEffectId = 15216;
    private const int PetSpeedBuffIconId = 3851;
    private const int PetSpeedBuffNameId = 116881;
    private const float PetSpeedBuffMovementSpeedBonus = 3f;
    private const int PetSpeedBuffDurationSeconds = 10;
    private const int PetSpeedBuffRefreshRemainingSeconds = 6;
    private const int PetTeleportCompositeEffectId = 46;
    private const int PetTrickIdeaCompositeEffectId = 5251;
    private const int PetTrickIdeaDelayMs = 2500;
    private const int PetTrickCheerBeforeFinishMs = 2000;
    private const int OwnerCheerAnimationId = 3306;
    private const int OwnerCongratsAnimationId = 3308;
    private static readonly int[] PetTrickOwnerCheerAnimationIds = [OwnerCheerAnimationId, OwnerCongratsAnimationId];
    private static readonly (int AnimationId, int DurationMs)[] PetDanceAnimations = [(PetDanceAnimationGroupId, PetDanceDurationMs)];
    private const int NoThoughtBubbleIconId = 0;
    private const float OwnerTeleportDistanceSquared = OwnerTeleportDistance * OwnerTeleportDistance;
    private const float PetGroundedPositionSpacingSquared = PetGroundedPositionSpacing * PetGroundedPositionSpacing;
    private const float PetNoticeRangeSquared = PetNoticeRange * PetNoticeRange;
    private const float PetCollectionNoticeRangeSquared = PetCollectionNoticeRange * PetCollectionNoticeRange;
    private const int PetTrickFinishedNotificationType = 0;
    private const int PetTrickStartedNotificationType = 1;
    private const int PetTrickUnlockedNotificationType = 3;
    private const int PetUtilityFeedStartedNotificationType = 1;
    private const int PetUtilityFeedFinishedNotificationType = 2;
    private const int PetUtilityGroomStartedNotificationType = 3;
    private const int PetUtilityGroomFinishedNotificationType = 4;
    private const int PetUtilityTrickStartedNotificationType = 5;
    private const int PetUtilityUnlockedNotificationType = 6;
    private const int PetCareUpdateMs = 1500;
    private const float PetPlayPerSecond = 1f / 6f;
    private const float PetScratchPlayPerMovement = 0.01f;
    private const float PetScratchMaxPlay = 2f;
    private const float PetMoodPerSecond = 1f;
    private const float PetMinCareStatForMood = 1f;
    private const float PetCareStatCount = 3f;

    public PlayerPet(Player player, IResourceManager resourceManager)
    {
        _player = player;
        _resourceManager = resourceManager;
    }

    public void SendPetList()
    {
        var petListPacket = new PetListPacket
        {
            ActivePetId = _player.ActivePetId,
            ActivePetGuid = _player.ActivePetGuid
        };
        petListPacket.Pets.AddRange(_player.Pets);
        _player.SendTunneled(petListPacket);
    }

    public void SummonPet(int id)
    {
        if (_player.Zone is not WorldZone && _player.Zone is not HousingZone)
            return;

        var petClientInfo = _player.GetPetClientInfo(id);

        if (petClientInfo is null || !_player.PetDefinitions.TryGetValue(id, out var definitionId) ||
            !_resourceManager.ClientItemDefinitions.TryGetValue(definitionId, out var clientItemDefinition) ||
            !PetHelper.TryGetModel(_resourceManager, clientItemDefinition, out var modelDefinition))
        {
            return;
        }

        RecallPet();

        if (!_player.Zone.TryCreateNpc(out var npc))
            return;

        npc.PositionSendIntervalMs = Npc.FollowerPositionIntervalMs;
        npc.Name = petClientInfo.Name;
        npc.NameId = petClientInfo.NameId;
        npc.ModelId = modelDefinition.Id;
        npc.TextureAlias = petClientInfo.TextureAlias;
        _resourceManager.Pets.TryGetValue(modelDefinition.Id, out var petDefinition);
        npc.TintAlias = petDefinition?.DefaultTintAlias is not null && petClientInfo.TintId == petDefinition.DefaultTintId
            ? petDefinition.DefaultTintAlias
            : clientItemDefinition.TintAlias;
        npc.TintId = petClientInfo.TintId;
        foreach (var itemGuid in petClientInfo.ItemGuids)
        {
            var clientItem = _player.Items.FirstOrDefault(item => item.Id == itemGuid);

            if (clientItem is not null && _resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition)
                && definition.Type == ItemDefinitionHelper.PetClothingItemType)
            {
                npc.Attachments.Add(PetHelper.GetAttachment(clientItem.Tint, definition));
            }
        }

        npc.IsInteractable = false;
        npc.Scale = modelDefinition.Scale > 0f ? modelDefinition.Scale : PetDefaultScale;
        npc.Disposition = PetNpcDisposition;
        npc.WaypointTolerance = PetWaypointTolerance;
        var followSide = GetRandomPetSide();
        var ownerPosition = _player.Position.ToVector3();
        var summonPosition = GetPetStandPosition(_player.Zone, ownerPosition,
            ownerPosition + Vector3.Cross(Vector3.UnitY, _player.GetFacingDirection()) * PetSummonSideDistance * followSide);
        npc.Visible = true;
        npc.UpdateEverySecondAction = () =>
        {
            if (ReferenceEquals(Pet, npc) && npc.Zone != _player.Zone)
                RecallPet();
        };
        npc.UpdateEveryTickAction = () => UpdatePetFollow(npc);

        _petState = new PetState
        {
            FollowSide = followSide,
            FollowGoal = summonPosition,
            LastPosition = summonPosition,
            OwnerLastPosition = ownerPosition,
            OwnerTickPosition = ownerPosition,
            IdleAnimationAt = Environment.TickCount64 + GetPetIdleAnimationDelayMs(),
            NoticeAt = Environment.TickCount64 + GetPetNoticeDelayMs(),
            CareUpdatedAt = Environment.TickCount64,
            LowestNeed = PetHelper.GetLowestNeed(petClientInfo)
        };

        Pet = npc;
        _player.ActivePetId = id;
        _player.ActivePetGuid = npc.Guid;
        _summonedPetId = id;
        npc.UpdatePosition(summonPosition.ToVector4(), _player.Rotation);
        SendPetList();

        _player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = npc.Guid,
            Unknown2 = _player.Guid,
            CompositeEffectId = PetTeleportCompositeEffectId,
            Position = npc.Position
        }, true);

        UpdatePetSpeedBuff();

        _petState.ThoughtBubbleAt = PetHelper.TrySendSummonThoughtBubble(_player, npc, petClientInfo, _petSpeedBuffEffectId != 0)
            ? GetNextPetThoughtBubbleAt()
            : 0;
    }

    public void SetPetUiMode(bool active)
    {
        _petState.UiModeActive = active;

        if (!active || Pet is null)
            return;

        StopPet(Pet);
        _petState.FaceOwnerAt = PetFaceOwnerDone;
    }

    public void ScratchPet(int inputMovementSquaredSum, int inputDurationMs)
    {
        if (Pet is null || inputMovementSquaredSum <= 0 || inputDurationMs <= 0)
            return;

        _petState.PendingPlay += Math.Min(PetScratchMaxPlay, inputMovementSquaredSum / (float)inputDurationMs * PetScratchPlayPerMovement);
    }

    public void QueueAction(Action action) => _queuedActions.Enqueue(action);

    public void UpdateEveryTick()
    {
        while (_queuedActions.TryDequeue(out var queuedAction))
            queuedAction();

        UpdatePetSpeedBuff();
        UpdatePetStand();
        UpdatePetCare();
    }

    private void UpdatePetCare()
    {
        if (Pet is null || Environment.TickCount64 - _petState.CareUpdatedAt < PetCareUpdateMs
            || _player.GetActivePetClientInfo() is not { } petClientInfo)
        {
            return;
        }

        var seconds = (Environment.TickCount64 - _petState.CareUpdatedAt) / (float)PetHelper.MillisecondsPerSecond;
        var play = Math.Min(DbPet.MaxNeed, petClientInfo.Play + seconds * PetPlayPerSecond + _petState.PendingPlay);

        var target = PetCareStatCount / (1f / Math.Max(petClientInfo.Hunger, PetMinCareStatForMood)
            + 1f / Math.Max(petClientInfo.Hygiene, PetMinCareStatForMood)
            + 1f / Math.Max(play, PetMinCareStatForMood));
        var mood = Math.Min(petClientInfo.Mood + seconds * PetMoodPerSecond, target);

        _petState.CareUpdatedAt = Environment.TickCount64;
        _petState.PendingPlay = 0f;

        if (play != petClientInfo.Play || mood != petClientInfo.Mood)
        {
            petClientInfo.Play = play;
            petClientInfo.Mood = mood;
            SendPetStatus(petClientInfo);
        }

        UpdatePetCareThoughtBubble(Pet, petClientInfo);
    }

    private void UpdatePetCareThoughtBubble(Npc npc, PetClientInfo petClientInfo)
    {
        var previousLowestNeed = _petState.LowestNeed;

        _petState.LowestNeed = PetHelper.GetLowestNeed(petClientInfo);

        if (Environment.TickCount64 >= _petState.ThoughtBubbleAt && PetHelper.TrySendCareThoughtBubble(_player, npc, petClientInfo, previousLowestNeed))
            _petState.ThoughtBubbleAt = GetNextPetThoughtBubbleAt();
    }

    public void SetPetInWater(bool inWater)
    {
        var npc = Pet;

        if (npc is null)
            return;

        var leftWater = _petState.InWater && !inWater;

        _petState.InWater = inWater;

        if (inWater)
            _petState.ShakeDurationMs = 0;

        if (!leftWater)
            return;

        if (IsPetModel(npc, PetHelper.PetDogModelPrefix))
            _petState.ShakeDurationMs = PetDogShakeDurationMs;
        else if (IsPetModel(npc, PetHelper.PetCatModelPrefix))
            _petState.ShakeDurationMs = PetCatShakeDurationMs;
    }

    private void UpdatePetShake(Npc npc)
    {
        if (_petState.ShakeAnimationAt != 0 && Environment.TickCount64 >= _petState.ShakeAnimationAt)
        {
            _petState.ShakeAnimationAt = 0;
            PlayPetAnimation(npc, PetDryOffAnimationGroupId, (int)(_petState.ShakeEndAt - Environment.TickCount64));
        }

        if (_petState.ShakeEndAt != 0 && Environment.TickCount64 >= _petState.ShakeEndAt)
        {
            _petState.ShakeEndAt = 0;
            _petActionActive = false;
        }

        var petPosition = npc.Position.ToVector3();

        if (_petState.ShakeDurationMs == 0 || _petActionActive || _petState.InWater || _petState.OwnerMoving
            || _petState.FaceOwnerAt == 0 || Vector3.DistanceSquared(petPosition, _petState.FollowGoal) >= PetStopArrivedDistanceSquared)
        {
            return;
        }

        _petState.ShakeAnimationAt = Environment.TickCount64 + PetStopSettleMs;
        _petState.ShakeEndAt = _petState.ShakeAnimationAt + _petState.ShakeDurationMs;
        _petState.ShakeDurationMs = 0;
        _petActionActive = true;
    }

    private void UpdatePetFollow(Npc npc)
    {
        if (!ReferenceEquals(Pet, npc))
            return;

        var ownerTickPosition = _player.Position.ToVector3();

        if (Vector3.DistanceSquared(ownerTickPosition, _petState.OwnerTickPosition) > OwnerTeleportDistanceSquared)
            _petState.OwnerTeleported = true;

        _petState.OwnerTickPosition = ownerTickPosition;

        UpdatePetShake(npc);
        UpdatePetTreasureDig(npc);

        if (_petActionActive || _petState.UiModeActive || npc.Zone != _player.Zone)
            return;

        if (UpdatePetRest(npc))
            return;

        if (UpdatePetInvestigate(npc))
            return;

        FollowOwner(npc);
        UpdatePetFind(npc);
        UpdatePetNotice(npc);
        UpdatePetIdleAnimation(npc);
    }

    private void UpdatePetIdleAnimation(Npc npc)
    {
        if (_petState.IdleAnimationEndAt != 0)
        {
            if (Environment.TickCount64 < _petState.IdleAnimationEndAt)
                return;

            _petState.IdleAnimationEndAt = 0;
        }

        if (_petState.OwnerMoving || _petState.FaceOwnerAt == 0 || _petState.InWater)
            return;

        var inDanceRange = BoomboxHelper.IsInDanceRange(_player);

        if (inDanceRange)
            _petState.IdleAnimationAt = Math.Min(_petState.IdleAnimationAt, Environment.TickCount64 + PetDanceJoinMaxDelayMs);

        if (Environment.TickCount64 < _petState.IdleAnimationAt)
            return;

        _petState.IdleAnimationAt = Environment.TickCount64 + (inDanceRange
            ? PetDanceDurationMs + Random.Shared.Next(PetDanceGapMinMs, PetDanceGapMaxMs)
            : GetPetIdleAnimationDelayMs());

        _resourceManager.Pets.TryGetValue(npc.ModelId, out var petDefinition);
        IReadOnlyList<(int AnimationId, int DurationMs)> idleAnimations;

        if (inDanceRange)
        {
            idleAnimations = PetDanceAnimations;
        }
        else if (petDefinition is { BathroomAnimations.Count: > 0 } && Random.Shared.NextSingle() < PetBathroomChance)
        {
            idleAnimations = GetIdleAnimations(petDefinition.BathroomAnimations);
        }
        else
        {
            var petIdleAnimations = petDefinition is null ? [] : GetIdleAnimations(petDefinition.IdleAnimations);

            if (_player.PetTraits.TryGetValue(_player.ActivePetId, out var petTrait) && PetTraitIdleAnimations.TryGetValue(petTrait, out var traitIdleAnimations)
                && IsPetModel(npc, PetHelper.PetDogModelPrefix))
            {
                petIdleAnimations.AddRange(traitIdleAnimations);
            }

            idleAnimations = petIdleAnimations;
        }

        if (idleAnimations.Count == 0)
            return;

        var (animationGroupId, durationMs) = PickIdleAnimation(idleAnimations);

        _petState.IdleAnimationEndAt = Environment.TickCount64 + durationMs;

        if (_petState.RoamAt != 0)
            _petState.RoamAt = Math.Max(_petState.RoamAt, _petState.IdleAnimationEndAt + GetPetRoamDelayMs());

        PlayPetAnimation(npc, animationGroupId, durationMs);
    }

    private (int AnimationId, int DurationMs) PickIdleAnimation(IReadOnlyList<(int AnimationId, int DurationMs)> animations)
    {
        var choiceCount = 0;

        foreach (var animation in animations)
        {
            if (animation.AnimationId != _petState.LastIdleAnimationId)
                choiceCount++;
        }

        var picked = animations[Random.Shared.Next(animations.Count)];

        if (choiceCount > 0)
        {
            var choice = Random.Shared.Next(choiceCount);

            foreach (var animation in animations)
            {
                if (animation.AnimationId == _petState.LastIdleAnimationId || choice-- > 0)
                    continue;

                picked = animation;
                break;
            }
        }

        _petState.LastIdleAnimationId = picked.AnimationId;
        return picked;
    }

    private bool IsPetIdleAnimationPlaying() => _petState.IdleAnimationEndAt != 0 && Environment.TickCount64 < _petState.IdleAnimationEndAt;

    private static int GetPetIdleAnimationDelayMs() => Random.Shared.Next(PetIdleAnimationMinDelayMs, PetIdleAnimationMaxDelayMs);

    private static int GetPetRoamDelayMs() => Random.Shared.Next(PetRoamMinDelayMs, PetRoamMaxDelayMs);

    private static int GetPetNoticeDelayMs() => Random.Shared.Next(PetNoticeMinDelayMs, PetNoticeMaxDelayMs);

    private static long GetNextPetThoughtBubbleAt() => Environment.TickCount64 + PetHelper.GetThoughtBubbleDelayMs();

    private static float GetRandomPetSide() => Random.Shared.NextSingle() < PetLeftSideChance ? PetLeftSide : PetRightSide;

    private static List<(int AnimationId, int DurationMs)> GetIdleAnimations(IEnumerable<PetIdleAnimationDefinition> idleAnimationDefinitions) =>
        idleAnimationDefinitions.Select(idleAnimation => (idleAnimation.AnimationId, idleAnimation.DurationMs)).ToList();

    private bool IsPetModel(Npc npc, string modelPrefix) =>
        _resourceManager.Models.TryGetValue(npc.ModelId, out var modelDefinition)
        && modelDefinition.ModelFileName.StartsWith(modelPrefix, StringComparison.OrdinalIgnoreCase);

    private void StartPetStay(Npc npc)
    {
        _petState.StayUntil = Environment.TickCount64 + Random.Shared.Next(PetStayMinMs, PetStayMaxMs);
        _petState.IdleAnimationAt = Environment.TickCount64 + GetPetIdleAnimationDelayMs();

        if (_petState.IdleAnimationEndAt != 0 || !_resourceManager.Pets.TryGetValue(npc.ModelId, out var petDefinition)
            || petDefinition.RestPostures.Count == 0 || Random.Shared.NextSingle() >= PetRestPostureChance)
        {
            return;
        }

        _petState.RestPosture = petDefinition.RestPostures[Random.Shared.Next(petDefinition.RestPostures.Count)];
        _petState.RestLoopAt = Environment.TickCount64 + _petState.RestPosture.EnterDurationMs;
        _petState.IdleAnimationAt = _petState.RestLoopAt + GetPetIdleAnimationDelayMs();

        EntityHelper.SetBaseAnimation(_player, npc, _petState.RestPosture.LoopAnimationIds[Random.Shared.Next(_petState.RestPosture.LoopAnimationIds.Count)]);
        EntityHelper.SetAnimation(_player, npc, _petState.RestPosture.EnterAnimationId);
    }

    private bool UpdatePetRest(Npc npc)
    {
        if (_petState.RestStandUntil != 0)
        {
            if (Environment.TickCount64 < _petState.RestStandUntil)
                return true;

            _petState.RestStandUntil = 0;
        }

        if (_petState.RestPosture is null)
            return false;

        var ownerMoved = Vector3.DistanceSquared(_player.Position.ToVector3(), _petState.OwnerLastPosition) > PetMovedDistanceSquared;

        if (ownerMoved || Environment.TickCount64 >= _petState.StayUntil)
        {
            _petState.RestStandUntil = Environment.TickCount64 + _petState.RestPosture.ExitDurationMs;
            _petState.StayUntil = 0;
            _petState.RestLoopAt = 0;
            EntityHelper.SetBaseAnimation(_player, npc, PetStandAnimationGroupId);
            PlayPetAnimation(npc, _petState.RestPosture.ExitAnimationId, _petState.RestPosture.ExitDurationMs);
            _petState.RestPosture = null;
            return true;
        }

        if (_petState.RestLoopAt != 0)
        {
            if (Environment.TickCount64 >= _petState.RestLoopAt)
                _petState.RestLoopAt = 0;

            return true;
        }

        if (Environment.TickCount64 >= _petState.IdleAnimationAt && _petState.RestPosture.FidgetAnimations.Count > 0)
        {
            var (fidgetAnimationId, fidgetDurationMs) = PickIdleAnimation(GetIdleAnimations(_petState.RestPosture.FidgetAnimations));

            _petState.RestLoopAt = Environment.TickCount64 + fidgetDurationMs;
            _petState.IdleAnimationAt = _petState.RestLoopAt + GetPetIdleAnimationDelayMs();
            EntityHelper.SetAnimation(_player, npc, fidgetAnimationId);
        }

        return true;
    }

    private void UpdatePetNotice(Npc npc)
    {
        if (_petState.NoticeLookUntil != 0 && Environment.TickCount64 >= _petState.NoticeLookUntil)
        {
            _petState.NoticeLookUntil = 0;
            _player.SendTunneledToVisible(new PlayerUpdatePacketSetLookAt { Guid = npc.Guid }, true);
        }

        if (IsPetIdleAnimationPlaying() || Environment.TickCount64 < _petState.NoticeAt || Environment.TickCount64 < _petState.ThoughtBubbleAt)
            return;

        _petState.NoticeAt = Environment.TickCount64 + GetPetNoticeDelayMs();

        var petPosition = npc.Position.ToVector3();
        var targetCount = 0;
        (ulong Guid, Vector3 Position, int IconId) target = default;

        foreach (var (_, visibleNpc) in _player.VisibleNpcs)
        {
            if (visibleNpc.Guid == npc.Guid || visibleNpc is Mount || visibleNpc is CollectionNode)
                continue;

            var visibleNpcPosition = visibleNpc.Position.ToVector3();

            if (Vector3.DistanceSquared(petPosition, visibleNpcPosition) > PetNoticeRangeSquared
                || !_resourceManager.Models.TryGetValue(visibleNpc.ModelId, out var modelDefinition))
            {
                continue;
            }

            var visibleNpcIconId = PetHelper.GetNoticeThoughtBubbleIconId(modelDefinition);

            if (visibleNpcIconId != NoThoughtBubbleIconId && Random.Shared.Next(++targetCount) == 0)
                target = (visibleNpc.Guid, visibleNpcPosition, visibleNpcIconId);
        }

        if (targetCount == 0)
            return;

        var (targetGuid, targetPosition, iconId) = target;

        _petState.NoticeLookUntil = Environment.TickCount64 + PetNoticeLookMs;
        _petState.ThoughtBubbleAt = GetNextPetThoughtBubbleAt();

        _player.SendTunneledToVisible(new PlayerUpdatePacketSetLookAt { Guid = npc.Guid, TargetGuid = targetGuid }, true);
        PetHelper.SendNoticeThoughtBubble(_player, npc, iconId);
        StartPetInvestigate(npc, targetPosition);
    }

    private void StartPetInvestigate(Npc npc, Vector3 targetPosition)
    {
        var petPosition = npc.Position.ToVector3();
        var fromTarget = new Vector3(petPosition.X - targetPosition.X, 0f, petPosition.Z - targetPosition.Z);
        var goal = fromTarget.LengthSquared() > 0f
            ? targetPosition + Vector3.Normalize(fromTarget) * PetInvestigateStopDistance
            : targetPosition;

        if (!npc.Zone.TryGetWalkableGoal(petPosition, goal, PetMaxStepUpHeight, PetMaxStepDownHeight, out goal))
            return;

        _petState.InvestigateGoal = goal;
        _petState.InvestigateGiveUpAt = Environment.TickCount64 + PetInvestigateTimeoutMs;
        _petState.IdleAnimationEndAt = 0;

        MovePet(npc, goal, _player.Stats[CharacterStatId.MaxMovementSpeed].Float);
    }

    private bool UpdatePetInvestigate(Npc npc)
    {
        if (_petState.InvestigateGoal is not { } goal)
            return false;

        var petPosition = npc.Position.ToVector3();
        var ownerPosition = _player.Position.ToVector3();

        if (Vector3.Distance(petPosition, ownerPosition) <= PetInvestigateMaxOwnerDistance
            && Environment.TickCount64 < _petState.InvestigateGiveUpAt && Vector3.Distance(petPosition, goal) > PetInvestigateStopDistance)
        {
            return true;
        }

        _petState.InvestigateGoal = null;

        if (Vector3.Distance(petPosition, goal) <= PetInvestigateStopDistance)
        {
            _petState.IdleAnimationEndAt = Environment.TickCount64 + PetNoticeLookMs;
            PlayPetAnimation(npc, PetSniffAnimationGroupId, PetNoticeLookMs);
        }

        return false;
    }

    private void UpdatePetFind(Npc npc)
    {
        if (IsPetIdleAnimationPlaying() || _player.GetActivePetClientInfo() is not { IsVanityPet: false } petClientInfo)
            return;

        UpdatePetTreasure(npc, petClientInfo);

        if (Environment.TickCount64 < _petState.ThoughtBubbleAt)
            return;

        var petPosition = npc.Position.ToVector3();
        var collectionNode = _player.VisibleNpcs.Values.OfType<CollectionNode>().FirstOrDefault(visibleCollectionNode =>
            !_petState.NoticedCollectionNodes.Contains(visibleCollectionNode.Guid)
            && Vector3.DistanceSquared(petPosition, visibleCollectionNode.Position.ToVector3()) <= PetCollectionNoticeRangeSquared);

        if (collectionNode is not null)
        {
            _petState.NoticedCollectionNodes.Add(collectionNode.Guid);
            _petState.ThoughtBubbleAt = GetNextPetThoughtBubbleAt();

            PetHelper.SendCollectionThoughtBubble(_player, npc);
            StartPetInvestigate(npc, collectionNode.Position.ToVector3());
        }
    }

    private void UpdatePetTreasure(Npc npc, PetClientInfo petClientInfo)
    {
        if (_petState.TreasurePending)
            return;

        if (!IsOnPetTreasureSpot() || petClientInfo.Mood < PetTreasureMinMood
            || _player.PetTreasureFoundAt.TryGetValue(_player.ActivePetId, out var treasureFoundAt) && DateTimeOffset.UtcNow - treasureFoundAt < PetTreasureCooldown)
        {
            _petState.TreasureAt = 0;
            return;
        }

        if (_petState.TreasureAt == 0)
        {
            _petState.TreasureAt = Environment.TickCount64 + Random.Shared.Next(PetTreasureMinDelayMs, PetTreasureMaxDelayMs);
            return;
        }

        if (Environment.TickCount64 < _petState.TreasureAt)
            return;

        _petState.TreasureAt = 0;
        _petState.TreasurePending = true;
        _petState.ThoughtBubbleAt = GetNextPetThoughtBubbleAt();

        PetHelper.SendTreasureThoughtBubble(_player, npc);

        _petState.TreasureDirection = _player.GetFacingDirection();
        _petState.TreasureSniffLegsLeft = PetTreasureSniffLegs;
        _petActionActive = true;
        EntityHelper.SetWalkAnimation(_player, npc, PetSniffWalkAnimationGroupId);
        StartPetTreasureSniff(npc);
    }

    private void StartPetTreasureSniff(Npc npc)
    {
        var petPosition = npc.Position.ToVector3();
        _petState.TreasureSniffLegsLeft--;

        for (var attempt = 0; attempt < PetRoamPickAttempts; attempt++)
        {
            var turn = (PetLeftSide + Random.Shared.NextSingle() * (PetRightSide - PetLeftSide)) * PetTreasureSniffMaxTurn;
            var distance = PetTreasureSniffMinDistance + Random.Shared.NextSingle() * (PetTreasureSniffMaxDistance - PetTreasureSniffMinDistance);
            var direction = Vector3.Transform(_petState.TreasureDirection, Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn));

            if (!npc.Zone.TryGetWalkableGoal(petPosition, petPosition + direction * distance, PetMaxStepUpHeight, PetMaxStepDownHeight, out var goal))
                continue;

            _petState.TreasureDirection = direction;
            _petState.TreasureDigAt = Environment.TickCount64 + (long)(distance / PetTreasureSniffSpeed * PetHelper.MillisecondsPerSecond);

            MovePet(npc, goal, PetTreasureSniffSpeed);
            return;
        }

        _petState.TreasureSniffLegsLeft = 0;
        _petState.TreasureDigAt = Environment.TickCount64;
    }

    private bool IsOnPetTreasureSpot()
    {
        var ownerPosition = _player.Position.ToVector3();

        return _player.Zone is WorldZone { Name: PetTreasureZoneName } && PetTreasurePointOfInterestIds.Any(pointOfInterestId =>
            _resourceManager.PointOfInterests.TryGetValue(pointOfInterestId, out var pointOfInterestDefinition)
            && ownerPosition.DistanceSquaredXZ(pointOfInterestDefinition.SpawnPosition.ToVector3()) <= PetTreasureSpotRangeSquared);
    }

    private void SpawnPetTreasureChest(Npc npc)
    {
        var roll = Random.Shared.Next(PetTreasureChestTotalWeight);
        var chestTier = PetTreasureChestTiers.First(tier => (roll -= tier.Weight) < 0);

        if (!_player.Zone.TryAddNpc((zone, guid) => new PetTreasureChest(zone, _player.Guid, chestTier.RewardTableKey) { Guid = guid }, out var chest))
            return;

        var petPosition = npc.Position.ToVector3();
        var position = GetPetStandPosition(npc.Zone, petPosition, petPosition + _petState.TreasureDirection * PetTreasureChestDistance);
        var despawnAt = Environment.TickCount64 + PetTreasureChestLifetimeMs;

        chest.NameId = chestTier.NameId;
        chest.ModelId = PetTreasureChestModelId;
        chest.TextureAlias = chestTier.TextureAlias;
        chest.InteractRange = PetTreasureChestInteractRange;
        chest.CursorId = PetTreasureChestCursorId;
        chest.CoinRewardTableKey = chestTier.CoinRewardTableKey;
        chest.OpenCompositeEffectId = chestTier.OpenCompositeEffectId;
        chest.HideNamePlate = true;
        chest.CompositeEffectId = PetTreasureChestDigUpCompositeEffectId;
        chest.Animation = PetTreasureChestStandAnimationId;
        chest.Visible = true;
        chest.UpdateEverySecondAction = () =>
        {
            if (Environment.TickCount64 >= despawnAt && chest.TryOpen())
                EntityHelper.RemovePlayerGracefully(chest, animate: true);
        };
        chest.UpdatePosition(position.ToVector4(), GetFacingOwnerRotation(position));

        EntityHelper.SetAnimation(_player, chest, PetTreasureChestSpawnAnimationId);

        _player.PetTreasureFoundAt[_player.ActivePetId] = DateTimeOffset.UtcNow;
    }

    private void UpdatePetTreasureDig(Npc npc)
    {
        if (!_petState.TreasurePending)
            return;

        if (_petState.TreasureDigAt != 0 && Environment.TickCount64 >= _petState.TreasureDigAt)
        {
            _petState.TreasureDigAt = 0;

            if (_petState.TreasureSniffLegsLeft > 0)
            {
                StartPetTreasureSniff(npc);
                return;
            }

            _petState.TreasureChestAt = Environment.TickCount64 + PetTreasureChestDigDelayMs;
            _petState.TreasureDigEndAt = Environment.TickCount64 + PetDigDurationMs;
            EntityHelper.SetWalkAnimation(_player, npc, PetWalkAnimationGroupId);
            PlayPetAnimation(npc, PetDiggingAnimationGroupId, PetDigDurationMs);
        }

        if (_petState.TreasureChestAt != 0 && Environment.TickCount64 >= _petState.TreasureChestAt)
        {
            _petState.TreasureChestAt = 0;
            SpawnPetTreasureChest(npc);
        }

        if (_petState.TreasureDigEndAt != 0 && Environment.TickCount64 >= _petState.TreasureDigEndAt)
        {
            _petState.TreasureDigEndAt = 0;
            _petState.TreasurePending = false;
            _petActionActive = false;
        }
    }

    private void FollowOwner(Npc npc)
    {
        var ownerPosition = _player.Position.ToVector3();
        var petPosition = npc.Position.ToVector3();
        var ownerSpeed = _player.Stats[CharacterStatId.MaxMovementSpeed].Float;

        if (Vector3.DistanceSquared(ownerPosition, _petState.OwnerLastPosition) > PetMovedDistanceSquared)
        {
            _petState.OwnerLastPosition = ownerPosition;
            _petState.OwnerMovedAt = Environment.TickCount64;
        }

        if ((_player.PositionState & (OwnerJumpingState | OwnerFallingState | OwnerGlidingState)) == 0)
        {
            RecordOwnerGroundedPosition(ownerPosition);
        }
        else
        {
            _petState.ChaseProgressAt = 0;

            if ((_player.PositionState & (OwnerFallingState | OwnerGlidingState)) != 0)
                MovePet(npc, GetOwnerTrailPosition(petPosition, PetTakeoffBackDistance), ownerSpeed);

            return;
        }

        if (_petState.OwnerTeleported)
        {
            _petState.OwnerTeleported = false;
            WarpPetBesideOwner(npc, ownerPosition);
            return;
        }

        var ownerDistance = Vector3.Distance(petPosition, ownerPosition);

        if (ownerDistance <= PetFollowWarpDistance)
        {
            _petState.ChaseProgressAt = 0;
        }
        else if (_petState.ChaseProgressAt == 0 || ownerDistance < _petState.ChaseClosestDistance - PetChaseProgressDistance)
        {
            _petState.ChaseClosestDistance = ownerDistance;
            _petState.ChaseProgressAt = Environment.TickCount64;
        }
        else if (Environment.TickCount64 - _petState.ChaseProgressAt > PetCannotCatchUpMs)
        {
            _petState.ChaseProgressAt = 0;
            WarpPetBesideOwner(npc, ownerPosition);
            return;
        }

        var ownerMoving = Environment.TickCount64 - _petState.OwnerMovedAt < PetOwnerStoppedDelayMs;

        var walkableToOwner = npc.Zone.HasGroundData
            && npc.Zone.IsWalkable(petPosition, ownerPosition, PetMaxStepUpHeight, PetMaxStepDownHeight, out _);

        if (!walkableToOwner && petPosition.Y - ownerPosition.Y > PetMaxStepDownHeight)
        {
            List<MapNode>? route = null;

            if (ownerMoving && (npc.Zone.HasNavMesh
                ? CanReachOverNavMesh(npc.Zone, petPosition, ownerPosition)
                : TryFindPetRoute(npc, petPosition, ownerPosition, out route)))
            {
                MovePet(npc, ownerPosition, ownerSpeed, route);
            }
            else
                WarpPetBesideOwner(npc, ownerPosition);

            return;
        }

        if (!walkableToOwner && ownerPosition.Y - petPosition.Y > PetWarpHeight)
        {
            WarpPetBesideOwner(npc, ownerPosition);
            return;
        }

        if (_petState.IdleAnimationEndAt != 0 && Environment.TickCount64 < _petState.IdleAnimationEndAt)
            return;

        if (ownerMoving || _petState.OwnerMoving)
        {
            Vector3 goal;

            if (ownerMoving)
            {
                var facing = _player.GetFacingDirection();
                var side = Vector3.Cross(Vector3.UnitY, facing);

                if (!_petState.OwnerMoving)
                    _petState.FollowSide = Vector3.Dot(petPosition - ownerPosition, side) >= 0f ? PetRightSide : PetLeftSide;

                goal = ownerPosition - facing * PetFollowBehindDistance + side * PetFollowSideDistance * _petState.FollowSide;

                if (!npc.Zone.TryGetWalkableGoal(ownerPosition, goal, PetMaxStepUpHeight, PetMaxStepDownHeight, out goal))
                    goal = GetOwnerTrailPosition(petPosition, PetFollowBehindDistance);
            }
            else
            {
                var angle = GetRandomPetSide() * (PetStopMinAngle + Random.Shared.NextSingle() * (PetStopMaxAngle - PetStopMinAngle));

                goal = ownerPosition + Vector3.Transform(_player.GetFacingDirection(), Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle)) * PetStopDistance;

                if (!npc.Zone.TryGetWalkableGoal(ownerPosition, goal, PetMaxStepUpHeight, PetMaxStepDownHeight, out goal)
                    && !npc.Zone.TryGetWalkableGoal(ownerPosition,
                        ownerPosition + Vector3.Transform(_player.GetFacingDirection(), Quaternion.CreateFromAxisAngle(Vector3.UnitY, -angle)) * PetStopDistance, PetMaxStepUpHeight, PetMaxStepDownHeight, out goal))
                {
                    goal = GetOwnerTrailPosition(petPosition, PetStopDistance);
                }
                _petState.RoamAt = 0;
                _petState.StayUntil = 0;
                _petState.RoamFirstHold = true;
            }

            var goalDistance = Vector3.Distance(petPosition, goal);

            if (goalDistance > PetCatchUpStartDistance)
                _petState.CatchingUp = true;
            else if (goalDistance < PetCatchUpStopDistance)
                _petState.CatchingUp = false;

            var followSpeed = _petState.CatchingUp
                ? MathF.Min(ownerSpeed + (goalDistance - PetCatchUpStopDistance) * PetCatchUpGain, ownerSpeed * PetCatchUpMaxSpeedMultiplier)
                : ownerSpeed;

            MovePet(npc, goal, followSpeed);

            _petState.OwnerMoving = ownerMoving;
            _petState.LastPosition = petPosition;
            _petState.FaceOwnerAt = 0;
            return;
        }

        if (_petState.RoamAt != 0 && Environment.TickCount64 >= _petState.RoamAt && _petState.IdleAnimationEndAt == 0)
        {
            _petState.RoamAt = Environment.TickCount64 + GetPetRoamDelayMs();
            _petState.StayUntil = 0;

            if (!_petState.InWater && Random.Shared.NextSingle() < PetStayChance)
            {
                StartPetStay(npc);
                _petState.RoamAt = _petState.StayUntil;
            }

            for (var attempt = 0; attempt < PetRoamPickAttempts && _petState.StayUntil == 0; attempt++)
            {
                var heading = Random.Shared.NextSingle() * MathF.Tau;
                var radius = PetRoamMinDistance + Random.Shared.NextSingle() * (PetRoamMaxDistance - PetRoamMinDistance);
                var goal = ownerPosition + new Vector3(MathF.Sin(heading), 0f, MathF.Cos(heading)) * radius;
                var distance = Vector3.Distance(petPosition, goal);

                if (distance < PetRoamMinMoveDistance || !IsPetPathClearOfOwner(petPosition, goal, ownerPosition)
                    || !npc.Zone.TryGetWalkableGoal(petPosition, goal, PetMaxStepUpHeight, PetMaxStepDownHeight, out goal))
                {
                    continue;
                }

                MovePet(npc, goal, distance > PetRoamMaxDistance ? ownerSpeed : PetRoamWalkSpeed);

                _petState.RoamAt = 0;
                _petState.LastPosition = petPosition;
                _petState.FaceOwnerAt = 0;
                return;
            }
        }

        var moved = Vector3.DistanceSquared(petPosition, _petState.LastPosition) > PetMovedDistanceSquared;
        _petState.LastPosition = petPosition;

        if (moved)
        {
            _petState.FaceOwnerAt = 0;
            return;
        }

        if (_petState.FaceOwnerAt == 0)
        {
            _petState.FaceOwnerAt = Environment.TickCount64 + Random.Shared.Next(PetFaceOwnerMinDelayMs, PetFaceOwnerMaxDelayMs);

            if (_petState.RoamAt == 0)
            {
                _petState.RoamAt = Environment.TickCount64 + (_petState.RoamFirstHold
                    ? Random.Shared.Next(PetRoamFirstMinDelayMs, PetRoamFirstMaxDelayMs)
                    : GetPetRoamDelayMs());
                _petState.RoamFirstHold = false;
            }

            return;
        }

        if (_petState.FaceOwnerAt == PetFaceOwnerDone || Environment.TickCount64 < _petState.FaceOwnerAt)
            return;

        _petState.FaceOwnerAt = PetFaceOwnerDone;
        npc.UpdatePosition(npc.Position, GetFacingOwnerRotation(petPosition));
    }

    private void RecordOwnerGroundedPosition(Vector3 ownerPosition)
    {
        var ownerGroundedPositions = _petState.OwnerGroundedPositions;

        if (ownerGroundedPositions.Count > 0 && Vector3.DistanceSquared(ownerGroundedPositions[^1], ownerPosition) < PetGroundedPositionSpacingSquared)
            return;

        ownerGroundedPositions.Add(ownerPosition);

        if (ownerGroundedPositions.Count > PetGroundedPositionCount)
            ownerGroundedPositions.RemoveAt(0);
    }

    private Vector3 GetOwnerTrailPosition(Vector3 petPosition, float backDistance)
    {
        var ownerGroundedPositions = _petState.OwnerGroundedPositions;

        if (ownerGroundedPositions.Count == 0)
            return petPosition;

        var takeoff = ownerGroundedPositions[^1];

        for (var index = ownerGroundedPositions.Count - OwnerTrailFirstSearchedFromEnd; index >= 0; index--)
        {
            var position = ownerGroundedPositions[index];

            if (position.DistanceSquaredXZ(takeoff) >= backDistance * backDistance)
                return position;
        }

        return ownerGroundedPositions[0];
    }

    private void WarpPetBesideOwner(Npc npc, Vector3 ownerPosition) =>
        WarpPet(npc, GetPetStandPosition(npc.Zone, ownerPosition,
            ownerPosition + Vector3.Cross(Vector3.UnitY, _player.GetFacingDirection()) * PetWarpSideDistance * _petState.FollowSide));

    private void WarpPet(Npc npc, Vector3 position)
    {
        if (_petState.StandAt != 0)
            StandPet(npc);

        var rotation = GetFacingOwnerRotation(position);

        npc.MoveTo(position, direct: true);
        npc.UpdatePosition(position.ToVector4(), rotation);

        foreach (var (_, visiblePlayer) in npc.VisiblePlayers)
        {
            visiblePlayer.OnRemoveVisibleNpcs(npc);
            visiblePlayer.OnAddVisibleNpcs(npc);
        }

        _petState.FollowGoal = position;
        _petState.LastPosition = position;
        _petState.FaceOwnerAt = PetFaceOwnerDone;
    }

    private Quaternion GetFacingOwnerRotation(Vector3 position)
    {
        var toOwner = _player.Position.ToVector3() - position;

        return (toOwner.DistanceSquaredXZ(Vector3.Zero) < PetFacingMinDistanceSquared ? _player.GetFacingDirection() : toOwner).ToFlatRotation();
    }

    private static bool IsPetPathClearOfOwner(Vector3 start, Vector3 goal, Vector3 ownerPosition)
    {
        var path = new Vector3(goal.X - start.X, 0f, goal.Z - start.Z);
        var toOwner = new Vector3(ownerPosition.X - start.X, 0f, ownerPosition.Z - start.Z);
        var along = Math.Clamp(Vector3.Dot(toOwner, path) / path.LengthSquared(), 0f, 1f);

        return Vector3.Distance(toOwner, path * along) >= PetRoamMinDistance;
    }

    private static Vector3 GetPetStandPosition(IZone zone, Vector3 ownerPosition, Vector3 position) =>
        zone.TryGetWalkableGoal(ownerPosition, position, PetMaxStepUpHeight, PetMaxStepDownHeight, out var standPosition) ? standPosition : ownerPosition;

    private bool CanReachOverNavMesh(IZone zone, Vector3 start, Vector3 goal) =>
        zone.TryFindReachableRoute(start, goal, _petNavMeshWaypoints, followRoads: true);

    private void MovePet(Npc npc, Vector3 goal, float speed, List<MapNode>? route = null)
    {
        _petState.FollowGoal = goal;

        if (_petState.StandAt != 0)
            StandPet(npc);

        EntityHelper.SetSpeed(_player, npc, speed);

        var petPosition = npc.Position.ToVector3();

        if (!npc.Zone.HasGroundData && goal.Y > petPosition.Y)
        {
            petPosition.Y = goal.Y;
            npc.SetPositionY(goal.Y);
        }

        var walkableToGoal = npc.Zone.HasGroundData
            && npc.Zone.IsWalkable(petPosition, goal, PetMaxStepUpHeight, PetMaxStepDownHeight, out _);

        if (route is null && !walkableToGoal && npc.Zone.HasNavMesh)
        {
            if (CanReachOverNavMesh(npc.Zone, petPosition, goal))
                npc.MoveTo(new Queue<Vector3>(_petNavMeshWaypoints));
            else if (Vector3.Distance(petPosition, _player.Position.ToVector3()) > PetCatchUpStartDistance)
                WarpPetBesideOwner(npc, _player.Position.ToVector3());
            else
                StopPet(npc);

            return;
        }

        if (route is null && !walkableToGoal && Vector3.DistanceSquared(petPosition, goal) > PetPathfindDistanceSquared)
            TryFindPetRoute(npc, petPosition, goal, out route);

        if (route is null)
            npc.MoveTo(goal, direct: true);
        else
            npc.MoveTo(goal, route);
    }

    private static bool TryFindPetRoute(Npc npc, Vector3 position, Vector3 goal, [NotNullWhen(true)] out List<MapNode>? route)
    {
        route = npc.Zone.Pathfinder?.FindPath(position, goal);

        if (route is [var firstNode, ..] && Vector3.DistanceSquared(position, firstNode.Position) <= PetPathfindMaxNodeDistanceSquared)
            return true;

        route = null;
        return false;
    }

    private static void StopPet(Npc npc) => npc.StopMoving();

    private void StartPetAction(Npc npc)
    {
        _petActionActive = true;
        _petState.StandAt = 0;
        StopPet(npc);
    }

    private void PlayPetAnimation(Npc npc, int animationGroupId, int durationMs)
    {
        EntityHelper.SetAnimation(_player, npc, animationGroupId);
        QueuePetStand(durationMs);
    }

    private void QueuePetStand(int delayMs) => _petState.StandAt = Environment.TickCount64 + delayMs;

    private void UpdatePetStand()
    {
        if (Pet is not { } npc || _petState.StandAt == 0 || Environment.TickCount64 < _petState.StandAt)
            return;

        StandPet(npc);
    }

    private void StandPet(Npc npc)
    {
        _petState.StandAt = 0;
        EntityHelper.SetAnimation(_player, npc, PetStandAnimationGroupId);
    }

    private void FinishPetAction(Npc npc)
    {
        npc.UpdateEveryTickAction = () => UpdatePetFollow(npc);
        _petActionActive = false;
    }

    public void PerformPetTrick(int trickId)
    {
        var npc = Pet;
        var petClientInfo = _player.GetActivePetClientInfo();

        if (npc is null || _petActionActive || _petState.InWater)
        {
            UnlockPetActions();
            return;
        }

        var petTrickInfo = petClientInfo?.PetTricks.FirstOrDefault(knownTrick => knownTrick.Id == trickId && knownTrick.Known);

        if (petTrickInfo is null || !_resourceManager.Pets.TryGetValue(npc.ModelId, out var petDefinition) ||
            !petDefinition.TrickAnimations.TryGetValue(trickId, out var petTrickDefinition))
        {
            UnlockPetActions();
            return;
        }

        StartPetAction(npc);
        _player.SendTunneledToVisible(new PlayerUpdatePacketSetLookAt { Guid = npc.Guid }, true);
        _player.SendTunneled(new PetTrickNotifyPacket
        {
            NotificationType = PetTrickStartedNotificationType,
            TrickId = trickId,
            NameId = petTrickInfo.NameId
        });

        if (petTrickDefinition.OwnerAnimationId != 0)
        {
            _player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
            {
                Guid = _player.Guid,
                AnimationId = petTrickDefinition.OwnerAnimationId
            }, true);
        }

        _player.SendTunneled(new PetUtilityNotifyPacket { PetId = _player.ActivePetId, NotificationType = PetUtilityTrickStartedNotificationType });
        var ideaAt = Environment.TickCount64 + PetTrickIdeaDelayMs;
        var startAt = Environment.TickCount64 + petTrickDefinition.StartDelayMs;
        var finishAt = startAt + petTrickDefinition.DurationMs;
        var cheerAt = finishAt - PetTrickCheerBeforeFinishMs;
        var ideaShown = false;
        var started = false;
        var cheered = false;

        npc.UpdateEveryTickAction = () =>
        {
            if (!ReferenceEquals(Pet, npc))
                return;

            if (!ideaShown && Environment.TickCount64 >= ideaAt)
            {
                ideaShown = true;
                _player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
                {
                    Guid = npc.Guid,
                    Unknown2 = _player.Guid,
                    CompositeEffectId = PetTrickIdeaCompositeEffectId
                }, true);
            }

            if (!started && Environment.TickCount64 >= startAt)
            {
                started = true;
                PlayPetAnimation(npc, petTrickDefinition.AnimationId, petTrickDefinition.DurationMs);
            }

            if (!cheered && Environment.TickCount64 >= cheerAt)
            {
                cheered = true;
                _player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
                {
                    Guid = _player.Guid,
                    AnimationId = PetTrickOwnerCheerAnimationIds[Random.Shared.Next(PetTrickOwnerCheerAnimationIds.Length)]
                }, true);
            }

            if (Environment.TickCount64 < finishAt)
                return;

            FinishPetAction(npc);
            _player.SendTunneled(new PetTrickNotifyPacket
            {
                NotificationType = PetTrickFinishedNotificationType,
                TrickId = trickId,
                NameId = petTrickInfo.NameId
            });
            UnlockPetActions();
            PlayPetAnimation(npc, PetVocalizePlayfulAnimationGroupId, PetVocalizePlayfulDurationMs);
        };
    }

    public void FeedPet(int foodBowlItemGuid, CharacterAttachmentData foodBowlAttachment, float hunger)
    {
        var npc = Pet;
        var petClientInfo = _player.GetActivePetClientInfo();

        if (npc is null || petClientInfo is not { IsVanityPet: false } || _petActionActive || _petState.InWater)
        {
            UnlockPetActions();
            return;
        }

        StartPetAction(npc);
        foodBowlAttachment.Slot = PetFoodBowlSlot;
        PetHelper.EquipItem(_player, npc, foodBowlItemGuid, foodBowlAttachment);
        EntityHelper.QueueAnimation(npc, PetFeedAnimationGroupId, loopCount: PetLoopOnce, unknown6: false, unknown7: true, unknown8: false);
        _player.SendTunneled(new PetUtilityNotifyPacket { PetId = _player.ActivePetId, NotificationType = PetUtilityFeedStartedNotificationType });
        var hungerAt = Environment.TickCount64 + PetFeedHungerDelayMs;
        var finishAt = Environment.TickCount64 + PetFeedDurationMs;
        var hungerSent = false;

        npc.UpdateEveryTickAction = () =>
        {
            if (!ReferenceEquals(Pet, npc))
                return;

            if (!hungerSent && Environment.TickCount64 >= hungerAt)
            {
                hungerSent = true;
                petClientInfo.Hunger = hunger;
                SendPetStatus(petClientInfo);
            }

            if (Environment.TickCount64 < finishAt)
                return;

            FinishPetAction(npc);
            QueuePetStand(PetStandNowMs);
            PetHelper.UnequipItem(_player, npc, PetFoodBowlSlot);
            _player.SendTunneled(new PetUtilityNotifyPacket { PetId = _player.ActivePetId, NotificationType = PetUtilityFeedFinishedNotificationType });
            UpdatePetHappyThoughtBubble(npc, petClientInfo);
        };
    }

    public void StartPetGrooming()
    {
        var npc = Pet;
        var petClientInfo = _player.GetActivePetClientInfo();

        if (npc is null || petClientInfo is not { IsVanityPet: false } || _petActionActive || _petState.InWater)
        {
            UnlockPetActions();
            return;
        }

        StartPetAction(npc);
        _petGroomingActive = true;
        EntityHelper.QueueAnimation(npc, PetEnjoyAnimationGroupId, loopCount: PetLoopForever, unknown6: true, unknown7: false, unknown8: true);
        _player.SendTunneled(new PetUtilityNotifyPacket
        {
            PetId = _player.ActivePetId,
            NotificationType = PetUtilityGroomStartedNotificationType,
            GroomDurationSeconds = PetGroomDurationSeconds
        });
        var finishAt = Environment.TickCount64 + PetGroomDurationSeconds * PetHelper.MillisecondsPerSecond;

        npc.UpdateEveryTickAction = () =>
        {
            if (ReferenceEquals(Pet, npc) && Environment.TickCount64 >= finishAt)
                FinishPetGrooming();
        };
    }

    public void GroomPet(int inputMovementSquaredSum)
    {
        var petClientInfo = _player.GetActivePetClientInfo();

        if (!_petGroomingActive || petClientInfo is null || inputMovementSquaredSum <= 0)
            return;

        petClientInfo.Hygiene = Math.Min(DbPet.MaxNeed, petClientInfo.Hygiene + inputMovementSquaredSum * PetGroomHygienePerMovement);
        SendPetStatus(petClientInfo);

        if (petClientInfo.Hygiene >= DbPet.MaxNeed)
            FinishPetGrooming();
    }

    private void FinishPetGrooming()
    {
        var npc = Pet;
        var petClientInfo = _player.GetActivePetClientInfo();

        if (npc is null || !_petGroomingActive)
            return;

        FinishPetAction(npc);
        _petGroomingActive = false;
        EntityHelper.QueueAnimation(npc, PetDryOffAnimationGroupId, loopCount: PetLoopOnce, unknown6: true, unknown7: true, unknown8: false);
        QueuePetStand(PetGroomDryOffDurationMs);
        _player.SendTunneled(new PetUtilityNotifyPacket { PetId = _player.ActivePetId, NotificationType = PetUtilityGroomFinishedNotificationType });

        if (petClientInfo is not null)
        {
            SendPetStatus(petClientInfo);
            UpdatePetHappyThoughtBubble(npc, petClientInfo);
        }
    }

    private void UpdatePetHappyThoughtBubble(Npc npc, PetClientInfo petClientInfo)
    {
        if (Environment.TickCount64 >= _petState.ThoughtBubbleAt && PetHelper.TrySendHappyThoughtBubble(_player, npc, petClientInfo))
            _petState.ThoughtBubbleAt = GetNextPetThoughtBubbleAt();
    }

    private void SendPetStatus(PetClientInfo petClientInfo)
    {
        _player.SendTunneled(new PetStatusPacket
        {
            PetId = petClientInfo.Id,
            Hunger = petClientInfo.Hunger,
            Hygiene = petClientInfo.Hygiene,
            Play = petClientInfo.Play,
            Mood = petClientInfo.Mood
        });

        UpdatePetSpeedBuff();
    }

    public void UpdatePetSpeedBuff()
    {
        var npc = Pet;
        var petClientInfo = _player.GetActivePetClientInfo();
        var active = npc is not null && petClientInfo is not null && !petClientInfo.IsVanityPet && petClientInfo.Mood >= PetHelper.PetHappyMinMood;

        if (!active || npc is null || Environment.TickCount64 < _petSpeedBuffRefreshAt)
            return;

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(PetSpeedBuffDurationSeconds);

        _petSpeedBuffRefreshAt = Environment.TickCount64 + (PetSpeedBuffDurationSeconds - PetSpeedBuffRefreshRemainingSeconds) * PetHelper.MillisecondsPerSecond;

        if (_petSpeedBuffEffectId != 0 && _player.RefreshEffect(_petSpeedBuffEffectId, expiresAt))
            return;

        _petSpeedBuffEffectId = _player.AddEffect(new PlayerEffect
        {
            ExpiresAt = expiresAt,
            WorldEffectId = PetSpeedBuffCompositeEffectId,
            BuffIconId = PetSpeedBuffIconId,
            BuffNameId = PetSpeedBuffNameId,
            BuffDefinitionId = PetSpeedBuffDefinitionId,
            SourceGuid = npc.Guid,
            MovementSpeedBonus = PetSpeedBuffMovementSpeedBonus
        });
    }

    public void UnlockPetActions()
    {
        _player.SendTunneled(new PetTrickNotifyPacket { PetId = _player.ActivePetId, NotificationType = PetTrickUnlockedNotificationType });
        _player.SendTunneled(new PetUtilityNotifyPacket { PetId = _player.ActivePetId, NotificationType = PetUtilityUnlockedNotificationType });
    }

    public void RestorePet()
    {
        if (_summonedPetId != 0 && Pet is null)
            SummonPet(_summonedPetId);
    }

    public void RecallPet(bool remember = false)
    {
        if (!remember)
            _summonedPetId = 0;

        var npc = DetachPet();

        if (npc is null)
            return;

        EntityHelper.ClearThoughtBubble(_player, npc);
        EntityHelper.RemovePlayerGracefully(npc, compositeEffectId: PetTeleportCompositeEffectId);

        _player.ActivePetId = 0;
        _player.ActivePetGuid = 0;
        _player.SendTunneled(new PetActivePacket());
        UpdatePetSpeedBuff();
    }

    public void Dispose() => DetachPet()?.Dispose();

    private Npc? DetachPet()
    {
        var npc = Pet;

        if (npc is not null)
        {
            npc.UpdateEverySecondAction = null;
            npc.UpdateEveryTickAction = null;
        }

        Pet = null;
        _petActionActive = false;
        _petGroomingActive = false;
        return npc;
    }
}
