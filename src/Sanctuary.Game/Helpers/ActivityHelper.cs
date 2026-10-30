using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.MiniGames;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class ActivityHelper
{
    public const int WorldServerType = 1;
    public const int ZoneServerType = 2;

    private const int DefaultEncounterDefinitionId = 1008;
    private const int DefaultDurationMs = 2000;
    private const int DefaultRespawnTime = 10000;
    public const int MiniGameAppSystemId = 1;
    private const int CombatMiniGameType = 4;

    public static void SendActivities(Player player, IResourceManager resourceManager)
    {
        SendActivities(player, resourceManager, ZoneServerType);
        SendActivities(player, resourceManager, WorldServerType);
    }

    public static void SendActivities(Player player, IResourceManager resourceManager, int serverType)
    {
        var activities = serverType == WorldServerType
            ? resourceManager.Activities.WorldServer
            : resourceManager.Activities.ZoneServer;

        player.SendTunneled(new ActivityPacketListOfActivities
        {
            ServerType = serverType,
            Activities = [.. activities.Select(clientActivityDefinition => GetPlayerActivity(player, resourceManager, clientActivityDefinition))]
        });
    }

    private static ClientActivityDefinition GetPlayerActivity(Player player, IResourceManager resourceManager, ClientActivityDefinition clientActivityDefinition)
    {
        if (!clientActivityDefinition.PlayerCanJoin || TryGetRequiredProfile(player, resourceManager, clientActivityDefinition, out _))
            return clientActivityDefinition;

        return new ClientActivityDefinition
        {
            Id = clientActivityDefinition.Id,
            AppSystemId = clientActivityDefinition.AppSystemId,
            Unknown3 = clientActivityDefinition.Unknown3,
            Category = clientActivityDefinition.Category,
            ImageSetId = clientActivityDefinition.ImageSetId,
            ImagePositionId = clientActivityDefinition.ImagePositionId,
            ServerType = clientActivityDefinition.ServerType,
            DisplayNameId = clientActivityDefinition.DisplayNameId,
            DisplayDescriptionId = clientActivityDefinition.DisplayDescriptionId,
            NameId = clientActivityDefinition.NameId,
            DescriptionId = clientActivityDefinition.DescriptionId,
            PlayerCanJoin = false,
            PreferredRequirementId = clientActivityDefinition.PreferredRequirementId,
            TutorialActivityId = clientActivityDefinition.TutorialActivityId,
            MembersOnly = clientActivityDefinition.MembersOnly,
            DetailImageFilename = clientActivityDefinition.DetailImageFilename,
            ThumbnailImageFilename = clientActivityDefinition.ThumbnailImageFilename,
            Difficulty = clientActivityDefinition.Difficulty,
            MysteryChestIcon = clientActivityDefinition.MysteryChestIcon,
            MysteryChestId = clientActivityDefinition.MysteryChestId,
            FeaturedActivities = clientActivityDefinition.FeaturedActivities
        };
    }

    public static MiniGameUiDefinition GetMiniGameUiDefinition(IResourceManager resourceManager, ClientActivityDefinition clientActivityDefinition)
    {
        if (MiniGameUiDefinitions.TryGet(resourceManager, clientActivityDefinition.Id, out var definition))
            return definition;

        var profileType = GetProfileType(resourceManager, clientActivityDefinition);

        if (profileType is null && TryGetMiniGameProfileType(resourceManager, clientActivityDefinition, out var miniGameProfileType))
            profileType = miniGameProfileType;

        MiniGameDefinition? miniGameDefinition = null;

        if (clientActivityDefinition.AppSystemId == MiniGameAppSystemId)
            resourceManager.MiniGames.TryGetValue(clientActivityDefinition.Unknown3, out miniGameDefinition);

        var miniGameType = profileType == ProfileType.Combat ? CombatMiniGameType : (int?)miniGameDefinition?.Type ?? default;

        return new MiniGameUiDefinition
        {
            GameId = clientActivityDefinition.Id,
            EncounterDefinitionId = DefaultEncounterDefinitionId,
            NameId = clientActivityDefinition.NameId,
            IconId = clientActivityDefinition.ImageSetId,
            DescriptionId = clientActivityDefinition.DescriptionId,
            Difficulty = clientActivityDefinition.Difficulty,
            Type = miniGameType,
            PreselectedGameId = miniGameDefinition?.Id ?? default,
            DurationMs = DefaultDurationMs,
            RespawnTime = DefaultRespawnTime,
            ShowStarCounter = true,
            ShowStatusIcon = true,
            ShowActionBar = true,
            StartupInCombat = true,
            ProfileType = profileType == ProfileType.Combat ? (int)ProfileType.Combat : default,
            MembersOnly = clientActivityDefinition.MembersOnly,
            ShowEndDialog = true,
            RewardBundle = RewardBundleBase.CreateEmpty(),
            MemberRewardBundle = RewardBundleBase.CreateEmpty(),
            PreviewRewardBundle = RewardBundleBase.CreateEmpty()
        };
    }

    private static ProfileType? GetProfileType(IResourceManager resourceManager, ClientActivityDefinition clientActivityDefinition)
    {
        if (!resourceManager.RequirementSets.TryGetValue(clientActivityDefinition.PreferredRequirementId, out var requirementSetDefinition))
            return null;

        return requirementSetDefinition.Groups
            .SelectMany(requirementGroupDefinition => requirementGroupDefinition.Requirements)
            .Select(rankRequirementDefinition => (ProfileType?)rankRequirementDefinition.ProfileType)
            .FirstOrDefault();
    }

    public static bool TryGetRequiredProfile(Player player, IResourceManager resourceManager, ClientActivityDefinition clientActivityDefinition, out ClientPcProfile? requiredProfile)
    {
        requiredProfile = null;

        if (!resourceManager.RequirementSets.TryGetValue(clientActivityDefinition.PreferredRequirementId, out var requirementSetDefinition))
        {
            if (!TryGetMiniGameProfileType(resourceManager, clientActivityDefinition, out var miniGameProfileType))
                return true;

            requiredProfile = GetProfile(player, miniGameProfileType);

            return requiredProfile is not null;
        }

        foreach (var requirementGroupDefinition in requirementSetDefinition.Groups)
        {
            if (requirementGroupDefinition.Requirements.Count == 0)
                continue;

            var profileType = requirementGroupDefinition.Requirements[0].ProfileType;
            var profile = GetProfile(player, profileType);

            if (profile is null)
                continue;

            if (requirementGroupDefinition.Requirements.All(rankRequirementDefinition => MeetsMinimumRank(profile, rankRequirementDefinition)))
            {
                requiredProfile = profile;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetMiniGameProfileType(IResourceManager resourceManager, ClientActivityDefinition clientActivityDefinition, out ProfileType profileType)
    {
        MiniGameDefinition? miniGameDefinition = null;

        if (clientActivityDefinition.AppSystemId == MiniGameAppSystemId)
            resourceManager.MiniGames.TryGetValue(clientActivityDefinition.Unknown3, out miniGameDefinition);

        return resourceManager.ActivityProfileTypes.TryGet(clientActivityDefinition, miniGameDefinition, out profileType);
    }

    private static ClientPcProfile? GetProfile(Player player, ProfileType profileType)
    {
        if (player.ActiveProfile.Type == (int)profileType)
            return player.ActiveProfile;

        return player.Profiles
            .Where(clientPcProfile => clientPcProfile.Type == (int)profileType)
            .MaxBy(clientPcProfile => clientPcProfile.Rank);
    }

    private static bool MeetsMinimumRank(ClientPcProfile profile, RankRequirementDefinition rankRequirementDefinition)
    {
        if (profile.Type != (int)rankRequirementDefinition.ProfileType)
            return false;

        if (rankRequirementDefinition.Operator != RequirementOperator.GreaterOrEqual)
            return true;

        return profile.Rank >= rankRequirementDefinition.Rank;
    }
}
