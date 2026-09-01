using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Core.Extensions;
using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Scripting;
using Sanctuary.UdpLibrary;
using Sanctuary.Game.Pathfinding;

namespace Sanctuary.Game.Zones;

[DebuggerDisplay("{Name} ({Id})")]
public abstract class BaseZone : IZone, IDisposable
{
    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IZoneManager _zoneManager;
    private readonly IScriptManager _scriptManager;
    private readonly ScriptRuntime _scriptRuntime;
    private readonly BaseZoneDefinition _zoneDefinition;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private const int VisibleTileRadius = 2;
    private readonly Dictionary<int, ZoneTile> _tiles;

    private readonly object _npcGuidLock = new();
    private ulong _nextNpcGuid = NpcBaseGuid;

    private readonly ConcurrentDictionary<ulong, Npc> _npcs = new();
    private readonly ConcurrentDictionary<ulong, Player> _players = new();
    private readonly ConcurrentDictionary<ulong, IEntity> _entities = new();
    private readonly object _collectionNodeLock = new();
    private readonly PriorityQueue<CollectionNodePoolRefill, long> _collectionNodeRefills = new();
    private readonly ConcurrentSet<string> _scripts = new();

    private const int FrameRate = 10;
    private const float TickRate = 1000f / FrameRate;

    public float TickDeltaSeconds => 1f / FrameRate;

    private const ulong NpcBaseGuid = 100_000_000_000u;

    private readonly record struct CollectionNodePoolRefill(string PoolKey, int CollectedHardPointId);

    private readonly PeriodicTimer _updateEveryTickTimer = new(TimeSpan.FromMilliseconds(TickRate));
    private readonly PeriodicTimer _updateEverySecondTimer = new(TimeSpan.FromSeconds(1));

    public int Id { get; init; }
    public int DefinitionId => _zoneDefinition.Id;
    public string Name => _zoneDefinition.Name;
    public ILogger Logger => _logger;

    public Vector4 SpawnPosition => _zoneDefinition.SpawnPosition;
    public Quaternion SpawnRotation => _zoneDefinition.SpawnRotation;

    public IEnumerable<Npc> Npcs => _npcs.Values;
    public IEnumerable<Player> Players => _players.Values;

    public bool IsEmpty => _players.IsEmpty;

    public IScriptManager ScriptManager => _scriptManager;
    public ScriptRuntime ScriptRuntime => _scriptRuntime;

    public Pathfinder<MapNode>? Pathfinder { get; }

    public ulong? OwnerId { get; init; }

    private bool _started = false;

    protected BaseZone(BaseZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
    {
        _zoneDefinition = zoneDefinition;
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger($"Zone {Name} ({Id})");

        _scriptManager = serviceProvider.GetRequiredService<IScriptManager>();
        _scriptRuntime = new ScriptRuntime(_logger);

        foreach (var script in _zoneDefinition.Scripts ?? [])
            _scripts.TryAdd(script);

        _tiles = GenerateTiles();

        foreach (var tile in _tiles)
        {
            ArgumentNullException.ThrowIfNull(tile.Value.Entities);
            ArgumentNullException.ThrowIfNull(tile.Value.VisibleTiles);
        }

        // Just in case we don't actually have the `.map` file for a particular zone.
        if (_resourceManager.Maps.TryGetValue(Name, out var mapGraph))
            Pathfinder = new Pathfinder<MapNode>(mapGraph.Nodes, _logger);
    }

    #region Events

    public virtual void OnStart()
    {
        if (_started)
            return;

        _started = true;

        GetOrCreateScriptContext().FireEvent("start");
        ActivateCollectionNodePools();

        Task.Factory.StartNew(UpdateEveryTickAsync, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task.Factory.StartNew(UpdateEverySecondAsync, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public virtual void OnClientIsReady(Player player)
    {
        SendQuickChatData(player);

        SendPointOfInterests(player);

        SendUpdateStat(player);

        var clientUpdatePacketHitpoints = new ClientUpdatePacketHitpoints
        {
            CurrentHitpoints = 2500,
            MaxHitpoints = 2500
        };

        player.SendTunneled(clientUpdatePacketHitpoints);

        var clientUpdatePacketMana = new ClientUpdatePacketMana
        {
            CurrentMana = 100,
            MaxMana = 100
        };

        player.SendTunneled(clientUpdatePacketMana);

        SendGuildData(player);

        SendReferenceData(player);

        SendCoinStoreItemList(player);

        SendAdventurersJournalInfo(player);

        SendWelcomeInfo(player);

        SendPlayerCustomizations(player);

        SendMembershipSubscriptionInfo(player);

        SendInGamePurchase(player);

        var packetZoneDoneSendingInitialData = new PacketZoneDoneSendingInitialData();

        player.SendTunneled(packetZoneDoneSendingInitialData);

        var clientUpdatePacketDoneSendingPreloadCharacters = new ClientUpdatePacketDoneSendingPreloadCharacters();

        player.SendTunneled(clientUpdatePacketDoneSendingPreloadCharacters);

        SendFriendList(player);
        SendIgnoreList(player);

        UpdateFriendStatus(player);
    }

    public virtual void OnClientFinishedLoading(Player player)
    {
    }

    private void SendQuickChatData(Player player)
    {
        var quickChatSendDataPacket = new QuickChatSendDataPacket();

        quickChatSendDataPacket.QuickChats = _resourceManager.QuickChats.ToDictionary();

        player.SendTunneled(quickChatSendDataPacket);
    }

    private void SendPointOfInterests(Player player)
    {
        var packetPointOfInterestDefinitionReply = new PacketPointOfInterestDefinitionReply();
        using var writer = new PacketWriter();

        foreach (var pointOfInterest in _resourceManager.PointOfInterests.Values)
        {
            writer.Write(true);

            pointOfInterest.Serialize(writer);
        }

        writer.Write(false);

        packetPointOfInterestDefinitionReply.Payload = writer.Buffer;

        player.SendTunneled(packetPointOfInterestDefinitionReply);
    }

    private void SendUpdateStat(Player player)
    {
        var clientUpdatePacketUpdateStat = new ClientUpdatePacketUpdateStat();

        clientUpdatePacketUpdateStat.Guid = player.Guid;

        // TODO
        clientUpdatePacketUpdateStat.Stats.AddRange(
        [
            new CharacterStat(CharacterStatId.MaxHealth, 2500),
            new CharacterStat(CharacterStatId.MaxMovementSpeed, 8f),
            new CharacterStat(CharacterStatId.WeaponRange, 5f),
            new CharacterStat(CharacterStatId.HitPointRegen, 25),
            new CharacterStat(CharacterStatId.MaxMana, 100),
            new CharacterStat(CharacterStatId.ManaRegen, 4),
            new CharacterStat(CharacterStatId.MeleeChanceToHit, 100),
            new CharacterStat(CharacterStatId.MeleeWeaponDamageMultiplier, 1f),
            new CharacterStat(CharacterStatId.MeleeHandToHandDamage, 1),
            new CharacterStat(CharacterStatId.EquippedMeleeWeaponDamage, 1),
            new CharacterStat(CharacterStatId.MeleeAttackIntervalMs, 2000),
            new CharacterStat(CharacterStatId.DamageMultiplier, 1f),
            new CharacterStat(CharacterStatId.HealingMultiplier, 1f),
            new CharacterStat(CharacterStatId.AbilityCriticalHitMultiplier, 1f),
            new CharacterStat(CharacterStatId.HeadInflationPercent, 100),
            new CharacterStat(CharacterStatId.RangeMultiplier, 1f),
            new CharacterStat(CharacterStatId.FactoryProductionModifier, 1f),
            new CharacterStat(CharacterStatId.FactoryYieldModifier, 1f),
            new CharacterStat(CharacterStatId.InCombatHitPointRegen, 6),
            new CharacterStat(CharacterStatId.InCombatManaRegen, 4)
        ]);

        player.SendTunneled(clientUpdatePacketUpdateStat);
    }

    private void SendGuildData(Player player)
    {
        var guildCanCreateGuildPacket = new GuildCanCreateGuildPacket
        {
            CanCreateGuild = player.Profiles.Any(x => x.Rank >= 15) && player.GuildData is null
        };

        player.SendTunneled(guildCanCreateGuildPacket);

        if (player.GuildData is null)
            return;

        var guildDataFullPacket = new GuildDataFullPacket
        {
            Data = player.GuildData,
            Guid = player.GuildData.Guid
        };

        player.SendTunneled(guildDataFullPacket);

        var guildPlayerStatusUpdatePacket = new GuildPlayerStatusUpdatePacket
        {
            PlayerGuid = player.Guid,
            GuildGuid = player.GuildData.Guid,
            IsInGuild = true
        };

        player.SendTunneled(guildPlayerStatusUpdatePacket);

        if (!player.GuildData.Members.TryGetValue(player.Guid, out var playerGuildMember))
            return;

        var guildMemberStatusUpdatePacket = new GuildMemberStatusUpdatePacket
        {
            GuildGuid = player.GuildData.Guid,
            MemberGuid = player.Guid,

            Name = player.Name,
            Role = playerGuildMember.Role,
            Online = true,

            Type = 6,

            WorldId = player.Zone.Id,

            ProfileId = player.ActiveProfileId,
            ProfileRank = player.ActiveProfile.Rank
        };

        foreach (var guildMember in player.GuildData.Members)
        {
            if (guildMember.Key == player.Guid)
                continue;

            if (!_zoneManager.TryGetPlayer(guildMember.Key, out var guildPlayer))
                continue;

            if (guildPlayer.GuildData is null)
                continue;

            if (guildPlayer.GuildData.Members.TryGetValue(player.Guid, out var onlineMember))
            {
                onlineMember.Online = true;
                onlineMember.WorldId = player.Zone.Id;
                onlineMember.ProfileId = player.ActiveProfileId;
                onlineMember.ProfileRank = player.ActiveProfile.Rank;
            }
            else
            {
                guildPlayer.GuildData.Members[player.Guid] = new GuildMember
                {
                    Guid = player.Guid,
                    Name = player.Name,
                    Role = playerGuildMember.Role,
                    Online = true,
                    WorldId = player.Zone.Id,
                    ProfileId = player.ActiveProfileId,
                    ProfileRank = player.ActiveProfile.Rank
                };
            }

            guildPlayer.SendTunneled(guildMemberStatusUpdatePacket);
        }
    }

    private void SendReferenceData(Player player)
    {
        var referenceDataPacketItemClassDefinitions = new ReferenceDataPacketItemClassDefinitions();

        referenceDataPacketItemClassDefinitions.ItemClasses = _resourceManager.ItemClasses.ToDictionary();

        player.SendTunneled(referenceDataPacketItemClassDefinitions);

        var referenceDataPacketItemCategoryDefinitions = new ReferenceDataPacketItemCategoryDefinitions();

        referenceDataPacketItemCategoryDefinitions.ItemCategories = _resourceManager.ItemCategories.ToDictionary();
        referenceDataPacketItemCategoryDefinitions.ItemCategoryGroups = _resourceManager.ItemCategoryGroups.ToDictionary();

        player.SendTunneled(referenceDataPacketItemCategoryDefinitions);

        var referenceDataPacketClientProfileData = new ReferenceDataPacketClientProfileData();

        referenceDataPacketClientProfileData.Profiles = _resourceManager.Profiles.ToDictionary();

        player.SendTunneled(referenceDataPacketClientProfileData);
    }

    private void SendCoinStoreItemList(Player player)
    {
        var coinStoreItemListPacket = new CoinStoreItemListPacket();

        coinStoreItemListPacket.StaticItems = _resourceManager.CoinStoreItems.ToDictionary();

        player.SendTunneled(coinStoreItemListPacket);

        var clientItemDefinitions = new List<ClientItemDefinition>();

        foreach (var coinStoreItem in _resourceManager.CoinStoreItems)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(coinStoreItem.Key, out var clientItemDefinition))
                continue;

            clientItemDefinitions.Add(clientItemDefinition);
        }

        using var writer = new PacketWriter();

        writer.Write(clientItemDefinitions);

        var playerUpdatePacketItemDefinitions = new PlayerUpdatePacketItemDefinitions();

        playerUpdatePacketItemDefinitions.Payload = writer.Buffer;

        player.SendTunneled(playerUpdatePacketItemDefinitions);
    }

    private void SendAdventurersJournalInfo(Player player)
    {
        // DO NOT REMOVE even if it's not fully implemented. This packet is needed
        // due to an Area Definition called "Newbiezone" in FabledRealmsAreas.xml.

        var adventurersJournal = new AdventurersJournalInfoPacket();

        AdventurersJournalRegionDefinition[] regions =
        [
            new()
            {
                Id = 1,
                NameId = 5100069,
                DescriptionId = 5100031,
                TabImageId = 35449,
                ChapterMapImageId = 0,
                GeometryId = 244,
                CompletedStringId = 5101408
            },
            new()
            {
                Id = 2,
                NameId = 442123,
                DescriptionId = 5100032,
                TabImageId = 9532,
                ChapterMapImageId = 0,
                GeometryId = 5,
                CompletedStringId = 442681,
            },
            new()
            {
                Id = 3,
                NameId = 3501,
                DescriptionId = 2129,
                TabImageId = 9538,
                ChapterMapImageId = 0,
                GeometryId = 8,
                CompletedStringId = 5101409,
            },
            new()
            {
                Id = 4,
                NameId = 3505,
                DescriptionId = 442685,
                TabImageId = 9529,
                ChapterMapImageId = 0,
                GeometryId = 1,
                CompletedStringId = 442686,
            }
        ];

        adventurersJournal.Regions = regions.ToDictionary(x => x.Id);

        AdventurersJournalHubDefinition[] hubs =
        [
            new()
            {
                Id = 1,
                RegionId = 1,
                DisplayOrder = 1,
                NameId = 442216,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 44310,
                CompletedDescriptionId = 5100071,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 2,
                RegionId = 1,
                DisplayOrder = 2,
                NameId = 18735,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 44311,
                CompletedDescriptionId = 5100072,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 3,
                RegionId = 1,
                DisplayOrder = 3,
                NameId = 5100069,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 44309,
                CompletedDescriptionId = 5100073,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 4,
                RegionId = 2,
                DisplayOrder = 1,
                NameId = 7262,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 44941,
                CompletedDescriptionId = 442125,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 5,
                RegionId = 2,
                DisplayOrder = 2,
                NameId = 428995,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 44942,
                CompletedDescriptionId = 442126,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 6,
                RegionId = 2,
                DisplayOrder = 3,
                NameId = 442124,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 44945,
                CompletedDescriptionId = 442127,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 7,
                RegionId = 2,
                DisplayOrder = 4,
                NameId = 4428,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 44943,
                CompletedDescriptionId = 442128,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 8,
                RegionId = 3,
                DisplayOrder = 1,
                NameId = 5101823,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 45267,
                CompletedDescriptionId = 5101824,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 9,
                RegionId = 3,
                DisplayOrder = 2,
                NameId = 5101825,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 45268,
                CompletedDescriptionId = 5101826,
                MapX = 0,
                MapY = 0
            },
            new()
            {
                Id = 10,
                RegionId = 4,
                DisplayOrder = 1,
                NameId = 442623,
                ActiveImageSetId = 19,
                ImageSetId = 44308,
                CompletedImageSetId = 45600,
                CompletedDescriptionId = 442687,
                MapX = 0,
                MapY = 0
            }
        ];

        adventurersJournal.Hubs = hubs.ToDictionary(x => x.Id);

        AdventurersJournalHubQuestDefinition[] hubQuests =
        [
            new()
            {
                HubId = 1,
                Id = 2514,
                Unknown = 2
            },
            new()
            {
                HubId = 1,
                Id = 2513,
                Unknown = 1
            },
            new()
            {
                HubId = 2,
                Id = 2521,
                Unknown = 2
            },
            new()
            {
                HubId = 2,
                Id = 2526,
                Unknown = 7
            },
            new()
            {
                HubId = 2,
                Id = 2522,
                Unknown = 3
            },
            new()
            {
                HubId = 2,
                Id = 2523,
                Unknown = 4
            },
            new()
            {
                HubId = 2,
                Id = 2524,
                Unknown = 5
            },
            new()
            {
                HubId = 2,
                Id = 2525,
                Unknown = 6
            },
            new()
            {
                HubId = 3,
                Id = 2529,
                Unknown = 3
            },
            new()
            {
                HubId = 3,
                Id = 2528,
                Unknown = 2
            },
            new()
            {
                HubId = 3,
                Id = 2527,
                Unknown = 1
            },
            new()
            {
                HubId = 3,
                Id = 2566,
                Unknown = 5
            },
            new()
            {
                HubId = 3,
                Id = 2530,
                Unknown = 4
            },
            new()
            {
                HubId = 4,
                Id = 2493,
                Unknown = 6
            },
            new()
            {
                HubId = 4,
                Id = 2492,
                Unknown = 5
            },
            new()
            {
                HubId = 4,
                Id = 2491,
                Unknown = 4
            },
            new()
            {
                HubId = 4,
                Id = 2490,
                Unknown = 3
            },
            new()
            {
                HubId = 4,
                Id = 2489,
                Unknown = 2
            },
            new()
            {
                HubId = 4,
                Id = 2538,
                Unknown = 1
            },
            new()
            {
                HubId = 5,
                Id = 2498,
                Unknown = 6
            },
            new()
            {
                HubId = 5,
                Id = 2497,
                Unknown = 5
            },
            new()
            {
                HubId = 5,
                Id = 2496,
                Unknown = 4
            },
            new()
            {
                HubId = 5,
                Id = 2495,
                Unknown = 3
            },
            new()
            {
                HubId = 5,
                Id = 2494,
                Unknown = 2
            },
            new()
            {
                HubId = 5,
                Id = 2531,
                Unknown = 1
            },
            new()
            {
                HubId = 6,
                Id = 2502,
                Unknown = 4
            },
            new()
            {
                HubId = 6,
                Id = 2501,
                Unknown = 3
            },
            new()
            {
                HubId = 6,
                Id = 2500,
                Unknown = 2
            },
            new()
            {
                HubId = 6,
                Id = 2499,
                Unknown = 1
            },
            new()
            {
                HubId = 6,
                Id = 2503,
                Unknown = 5
            },
            new()
            {
                HubId = 7,
                Id = 2533,
                Unknown = 7
            },
            new()
            {
                HubId = 7,
                Id = 2532,
                Unknown = 1
            },
            new()
            {
                HubId = 7,
                Id = 2504,
                Unknown = 2
            },
            new()
            {
                HubId = 7,
                Id = 2508,
                Unknown = 6
            },
            new()
            {
                HubId = 7,
                Id = 2507,
                Unknown = 5
            },
            new()
            {
                HubId = 7,
                Id = 2505,
                Unknown = 3
            },
            new()
            {
                HubId = 7,
                Id = 2506,
                Unknown = 4
            },
            new()
            {
                HubId = 8,
                Id = 2580,
                Unknown = 5
            },
            new()
            {
                HubId = 8,
                Id = 2578,
                Unknown = 3
            },
            new()
            {
                HubId = 8,
                Id = 2579,
                Unknown = 4
            },
            new()
            {
                HubId = 8,
                Id = 2577,
                Unknown = 2
            },
            new()
            {
                HubId = 8,
                Id = 2576,
                Unknown = 1
            },
            new()
            {
                HubId = 9,
                Id = 2585,
                Unknown = 10
            },
            new()
            {
                HubId = 9,
                Id = 2584,
                Unknown = 9
            },
            new()
            {
                HubId = 9,
                Id = 2583,
                Unknown = 8
            },
            new()
            {
                HubId = 9,
                Id = 2582,
                Unknown = 7
            },
            new()
            {
                HubId = 9,
                Id = 2581,
                Unknown = 6
            },
            new()
            {
                HubId = 9,
                Id = 2600,
                Unknown = 11
            },
            new()
            {
                HubId = 10,
                Id = 2595,
                Unknown = 6
            },
            new()
            {
                HubId = 10,
                Id = 2594,
                Unknown = 5
            },
            new()
            {
                HubId = 10,
                Id = 2591,
                Unknown = 4
            },
            new()
            {
                HubId = 10,
                Id = 2590,
                Unknown = 3
            },
            new()
            {
                HubId = 10,
                Id = 2596,
                Unknown = 7
            },
            new()
            {
                HubId = 10,
                Id = 2588,
                Unknown = 1
            },
            new()
            {
                HubId = 10,
                Id = 2599,
                Unknown = 10
            },
            new()
            {
                HubId = 10,
                Id = 2598,
                Unknown = 9
            },
            new()
            {
                HubId = 10,
                Id = 2597,
                Unknown = 8
            },
            new()
            {
                HubId = 10,
                Id = 2589,
                Unknown = 2
            }
        ];

        adventurersJournal.HubQuests = hubQuests.ToDictionary(x => x.Id);

        AdventurersJournalStickerDefinition[] stickers =
        [
            new()
            {
                Id = 1,
                RegionId = 1,
                DisplayOrder = 1,
                QuestId = 2563,
                NameId = 5100479,
                DescriptionId = 5100480,
                CompletedImageSetId = 43279,
                ImageSetId = 43278,
                Unknown = 0
            },
            new()
            {
                Id = 2,
                RegionId = 1,
                DisplayOrder = 2,
                QuestId = 2564,
                NameId = 5100483,
                DescriptionId = 5100484,
                CompletedImageSetId = 43287,
                ImageSetId = 43286,
                Unknown = 0
            },
            new()
            {
                Id = 3,
                RegionId = 1,
                DisplayOrder = 3,
                QuestId = 2565,
                NameId = 5100487,
                DescriptionId = 5100488,
                CompletedImageSetId = 43273,
                ImageSetId = 43272,
                Unknown = 0
            },
            new()
            {
                Id = 4,
                RegionId = 1,
                DisplayOrder = 4,
                QuestId = 2572,
                NameId = 5100772,
                DescriptionId = 5100773,
                CompletedImageSetId = 43281,
                ImageSetId = 43280,
                Unknown = 0
            },
            new()
            {
                Id = 5,
                RegionId = 1,
                DisplayOrder = 5,
                QuestId = 2573,
                NameId = 5100776,
                DescriptionId = 5100777,
                CompletedImageSetId = 43291,
                ImageSetId = 43290,
                Unknown = 0
            },
            new()
            {
                Id = 6,
                RegionId = 1,
                DisplayOrder = 6,
                QuestId = 2587,
                NameId = 5101187,
                DescriptionId = 5101188,
                CompletedImageSetId = 43283,
                ImageSetId = 43282,
                Unknown = 0
            },
            new()
            {
                Id = 16,
                RegionId = 2,
                DisplayOrder = 1,
                QuestId = 2568,
                NameId = 5100756,
                DescriptionId = 5100757,
                CompletedImageSetId = 43305,
                ImageSetId = 43304,
                Unknown = 0
            },
            new()
            {
                Id = 17,
                RegionId = 2,
                DisplayOrder = 2,
                QuestId = 2569,
                NameId = 5100760,
                DescriptionId = 5100761,
                CompletedImageSetId = 43287,
                ImageSetId = 43286,
                Unknown = 0
            },
            new()
            {
                Id = 18,
                RegionId = 2,
                DisplayOrder = 3,
                QuestId = 2570,
                NameId = 5100764,
                DescriptionId = 5100765,
                CompletedImageSetId = 43273,
                ImageSetId = 43272,
                Unknown = 0
            },
            new()
            {
                Id = 19,
                RegionId = 2,
                DisplayOrder = 4,
                QuestId = 2571,
                NameId = 5100768,
                DescriptionId = 5100769,
                CompletedImageSetId = 43279,
                ImageSetId = 43278,
                Unknown = 0
            },
            new()
            {
                Id = 20,
                RegionId = 2,
                DisplayOrder = 5,
                QuestId = 2574,
                NameId = 5100780,
                DescriptionId = 5100781,
                CompletedImageSetId = 43277,
                ImageSetId = 43276,
                Unknown = 0
            },
            new()
            {
                Id = 21,
                RegionId = 2,
                DisplayOrder = 6,
                QuestId = 2575,
                NameId = 5100784,
                DescriptionId = 5100785,
                CompletedImageSetId = 43283,
                ImageSetId = 43282,
                Unknown = 0
            },
            new()
            {
                Id = 32,
                RegionId = 3,
                DisplayOrder = 2,
                QuestId = 2602,
                NameId = 442851,
                DescriptionId = 442857,
                CompletedImageSetId = 43287,
                ImageSetId = 43286,
                Unknown = 0
            },
            new()
            {
                Id = 35,
                RegionId = 3,
                DisplayOrder = 5,
                QuestId = 2605,
                NameId = 442854,
                DescriptionId = 442860,
                CompletedImageSetId = 43279,
                ImageSetId = 43278,
                Unknown = 0
            },
            new()
            {
                Id = 36,
                RegionId = 3,
                DisplayOrder = 6,
                QuestId = 2606,
                NameId = 442855,
                DescriptionId = 442861,
                CompletedImageSetId = 43305,
                ImageSetId = 43304,
                Unknown = 0
            },
            new()
            {
                Id = 37,
                RegionId = 4,
                DisplayOrder = 1,
                QuestId = 2592,
                NameId = 0,
                DescriptionId = 0,
                CompletedImageSetId = 0,
                ImageSetId = 0,
                Unknown = 0
            }
        ];

        adventurersJournal.Stickers = stickers.ToDictionary(x => x.Id);

        player.SendTunneled(adventurersJournal);
    }

    private void SendWelcomeInfo(Player player)
    {
        var packetLoadWelcomeScreen = new PacketLoadWelcomeScreen();

        packetLoadWelcomeScreen.Contents.AddRange(
        [
            new ContentInfo
            {
                NameId = 6185,
                DescriptionId = 6186,
            },
            new ContentInfo
            {
                NameId = 6187,
                DescriptionId = 6188,
            },
            new ContentInfo
            {
                NameId = 6189,
                DescriptionId = 6190,
            }
        ]);

        packetLoadWelcomeScreen.ClaimCodes.AddRange(
        [
            new ClaimCodeInfo
            {
                Code = "MMMDONUT",
                NameId = 401519,
                DescriptionId = 401534,
                IconId = 929
            },
            new ClaimCodeInfo
            {
                Code = "BERRYCUPCAKE",
                NameId = 401517,
                DescriptionId = 401532,
                IconId = 939
            },
            new ClaimCodeInfo
            {
                Code = "SKELETAL",
                NameId = 409157,
                DescriptionId = 109132,
                IconId = 3459
            },
            new ClaimCodeInfo
            {
                Code = "STRAWBERRIES",
                NameId = 409158,
                DescriptionId = 108948,
                IconId = 3441
            },
            new ClaimCodeInfo
            {
                Code = "FROGGY",
                NameId = 409159,
                DescriptionId = 3141,
                IconId = 1258
            },
            new ClaimCodeInfo
            {
                Code = "SANDWICH",
                NameId = 409160,
                DescriptionId = 2430,
                IconId = 949
            }
        ]);

        player.SendTunneled(packetLoadWelcomeScreen);
    }

    private void SendPlayerCustomizations(Player player)
    {
        var playerUpdatePacketCustomizationData = new PlayerUpdatePacketCustomizationData();

        var customizations = new[]
        {
            new PlayerCustomizationData
            {
                Id = 0, // Head
                Param = player.HeadId,
                StringParam = player.Head
            },
            new PlayerCustomizationData
            {
                Id = 1, // Skin Tone
                Param = player.SkinToneId,
                StringParam = player.SkinTone
            },
            new PlayerCustomizationData
            {
                Id = 2, // Hair
                Param = player.HairId,
                StringParam = player.Hair
            },
            new PlayerCustomizationData
            {
                Id = 3, // Hair Color
                Param = player.HairColor
            },
            new PlayerCustomizationData
            {
                Id = 4, // Eye Color
                Param = player.EyeColor
            },
            new PlayerCustomizationData
            {
                Id = 5, // Model Customization
                Param = player.ModelCustomizationId,
                StringParam = player.ModelCustomization
            },
            new PlayerCustomizationData
            {
                Id = 6, // Face Paint
                Param = player.FacePaintId,
                StringParam = player.FacePaint
            },
            new PlayerCustomizationData
            {
                Id = 8, // Model
                Param = player.Model
            }
        };

        playerUpdatePacketCustomizationData.Customizations.AddRange(customizations);

        player.SendTunneled(playerUpdatePacketCustomizationData);
    }

    private void SendMembershipSubscriptionInfo(Player player)
    {
        var packetMembershipSubscriptionInfo = new PacketMembershipSubscriptionInfo
        {
            IsMember = player.MembershipStatus != 0
        };

        player.SendTunneled(packetMembershipSubscriptionInfo);
    }

    private void SendInGamePurchase(Player player)
    {
        var packetInGamePurchaseEnableMarketplace = new PacketInGamePurchaseEnableMarketplace
        {
            Enabled = true
        };

        player.SendTunneled(packetInGamePurchaseEnableMarketplace);

        var packetInGamePurchaseStoreEnablePaymentSources = new PacketInGamePurchaseStoreEnablePaymentSources
        {
            Sms = true,
            Paypal = true
        };

        player.SendTunneled(packetInGamePurchaseStoreEnablePaymentSources);

        var packetInGamePurchaseStoreBundleCategoryGroups = new PacketInGamePurchaseStoreBundleCategoryGroups();

        packetInGamePurchaseStoreBundleCategoryGroups.CategoryGroups = _resourceManager.StoreBundleCategoryGroups.ToDictionary();

        player.SendTunneled(packetInGamePurchaseStoreBundleCategoryGroups);

        var packetInGamePurchaseStoreBundleCategories = new PacketInGamePurchaseStoreBundleCategories();

        packetInGamePurchaseStoreBundleCategories.CategoryTree.Categories = _resourceManager.StoreBundleCategories.ToDictionary();

        player.SendTunneled(packetInGamePurchaseStoreBundleCategories);

        if (_resourceManager.Stores.TryGetValue(1, out var mainStore))
        {
            var packetInGamePurchaseStoreBundles = new PacketInGamePurchaseStoreBundles();

            packetInGamePurchaseStoreBundles.StoreId = mainStore.Id;

            packetInGamePurchaseStoreBundles.Store.Id = mainStore.Id;
            packetInGamePurchaseStoreBundles.Store.NameId = mainStore.NameId;
            packetInGamePurchaseStoreBundles.Store.DescriptionId = mainStore.DescriptionId;
            packetInGamePurchaseStoreBundles.Store.Image = mainStore.Image;

            foreach (var storeBundle in mainStore.Bundles.Values)
            {
                var valid = storeBundle.Entries.All(x => _resourceManager.ClientItemDefinitions.ContainsKey(x.MarketingItemId));

                if (valid)
                    packetInGamePurchaseStoreBundles.Store.Bundles.Add(storeBundle.Id, storeBundle);
            }

            player.SendTunneled(packetInGamePurchaseStoreBundles);
        }

        var packetInGamePurchaseStoreBundleGroups = new PacketInGamePurchaseStoreBundleGroups();

        packetInGamePurchaseStoreBundleGroups.BundleGroups = _resourceManager.StoreBundleGroups.ToDictionary();

        player.SendTunneled(packetInGamePurchaseStoreBundleGroups);
    }

    private void SendFriendList(Player player)
    {
        var friendListPacket = new FriendListPacket();

        friendListPacket.Friends = player.Friends;

        player.SendTunneled(friendListPacket);
    }

    private void SendIgnoreList(Player player)
    {
        var ignoreListPacket = new IgnoreListPacket();

        ignoreListPacket.Ignores = player.Ignores;

        player.SendTunneled(ignoreListPacket);
    }

    private void UpdateFriendStatus(Player player)
    {
        var friendOnlinePacket = new FriendOnlinePacket();

        friendOnlinePacket.Guid = player.Guid;

        friendOnlinePacket.IsLocal = true;

        var friendStatusPacket = new FriendStatusPacket
        {
            Guid = player.Guid,
            Status =
            {
                ProfileId = player.ActiveProfile.Id,
                ProfileRank = player.ActiveProfile.Rank,
                ProfileIconId = player.ActiveProfile.Icon,
                ProfileNameId = player.ActiveProfile.NameId,
                ProfileBackgroundImageId = player.ActiveProfile.BadgeImageSet
            }
        };

        foreach (var friend in player.Friends)
        {
            if (!_zoneManager.TryGetPlayer(friend.Guid, out var friendPlayer))
                continue;

            var otherFriendPlayer = friendPlayer.Friends.FirstOrDefault(x => x.Guid == player.Guid);

            if (otherFriendPlayer is null || otherFriendPlayer.Online)
                continue;

            otherFriendPlayer.Online = true;

            friendPlayer.SendTunneled(friendOnlinePacket);
            friendPlayer.SendTunneled(friendStatusPacket);
        }
    }

    #endregion

    #region IScriptable

    public ScriptContext GetOrCreateScriptContext()
    {
        if (_scriptManager.GetOrCreateContext(this, out var context))
        {
            // Fresh context. Attach all scripts defined in the zone definition.
            // We can't use `LoadScriptInBackground` here because we need to ensure that any `onStart` handlers are fully loaded.
            foreach (var script in _scripts)
                context.LoadScript(Path.Combine("Zone", script + ".lua"));
        }

        return context;
    }

    public bool TryAddScript(string scriptName)
    {
        var context = GetOrCreateScriptContext();

        if (!_scripts.TryAdd(scriptName))
            return false;

        var scriptPath = Path.Combine("Zone", scriptName + ".lua");

        context.LoadScriptInBackground(scriptPath);

        return true;
    }

    public bool TryRemoveScript(string scriptName)
    {
        if (!_scripts.TryRemove(scriptName))
            return false;

        var context = GetOrCreateScriptContext();

        var scriptPath = Path.Combine("Zone", scriptName + ".lua");

        return context.UnloadScript(scriptPath);
    }

    #endregion

    #region Scripting API

    public bool TrySpawnNpc(int npcId, ulong? npcGuid, float x, float y, float z, float heading, [MaybeNullWhen(false)] out IScriptableNpc npc)
    {
        npc = null;

        if (npcGuid.HasValue)
        {
            if (_npcs.ContainsKey(npcGuid.Value))
            {
                _logger.LogWarning("Failed to spawn NPC {NpcId} with GUID {NpcGuid}: GUID already exists.", npcId, npcGuid.Value);
                return false;
            }
        }

        var definition = _resourceManager.Npcs.Values.FirstOrDefault(n => n.Id == npcId);
        if (definition is null)
        {
            _logger.LogWarning("Failed to spawn NPC {NpcId}: No definition found.", npcId);
            return false;
        }

        if (!TryCreateNpc(npcGuid, definition, out var spawnedNpc))
        {
            _logger.LogWarning("Failed to spawn NPC {NpcId}: Could not create NPC instance.", npcId);
            return false;
        }

        var position = new Vector4(x, y, z, 1f);
        var rotation = new Quaternion(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f);

        spawnedNpc.UpdatePosition(position, rotation);

        npc = spawnedNpc;
        return true;
    }

    #endregion

    #region Entities

    public bool TryGetNpc(ulong guid, [MaybeNullWhen(false)] out Npc npc)
    {
        return _npcs.TryGetValue(guid, out npc);
    }

    public bool TryGetPlayer(ulong guid, [MaybeNullWhen(false)] out Player player)
    {
        return _players.TryGetValue(guid, out player);
    }

    public bool TryGetEntity(ulong guid, [MaybeNullWhen(false)] out IEntity entity)
    {
        return _entities.TryGetValue(guid, out entity);
    }

    public bool TryAddMount(Mount mount)
    {
        return _npcs.TryAdd(mount.Guid, mount) && _entities.TryAdd(mount.Guid, mount);
    }

    public bool TryAddPlayer(Player player)
    {
        return _players.TryAdd(player.Guid, player) && _entities.TryAdd(player.Guid, player);
    }

    private bool TryRegisterEntity<TEntity>(ConcurrentDictionary<ulong, TEntity> collection, TEntity entity)
        where TEntity : IEntity
    {
        if (!collection.TryAdd(entity.Guid, entity))
            return false;

        if (!_entities.TryAdd(entity.Guid, entity))
        {
            collection.TryRemove(entity.Guid, out _);
            return false;
        }

        return true;
    }

    public bool TryCreateNpc(ulong? guid, [MaybeNullWhen(false)] out Npc npc)
    {
        npc = new Npc(this)
        {
            Guid = GetNpcGuid(guid)
        };

        if (!TryRegisterEntity(_npcs, npc))
        {
            npc = null;
            return false;
        }

        return true;
    }

    public bool TryCreateNpc(ulong? guid, NpcDefinition definition, [MaybeNullWhen(false)] out Npc npc)
    {
        var scale = 1f;

        if (_resourceManager.Models.TryGetValue(definition.ModelId, out var model) && model.Scale != 0f)
            scale = model.Scale;

        npc = new Npc(this)
        {
            Guid = GetNpcGuid(guid),
            NameId = definition.NameId,
            Name = definition.Name,
            ModelId = definition.ModelId,
            TextureAlias = definition.TextureAlias,
            Scale = scale,
            Visible = true
        };

        if (!TryRegisterEntity(_npcs, npc))
        {
            npc = null;
            return false;
        }

        foreach (var script in definition.Scripts ?? [])
        {
            if (!npc.TryAddScript(script))
                _logger.LogWarning("NPC {NpcName} ({NpcGuid}) already has script {Script}", npc.Name, npc.Guid, script);
        }

        return true;
    }

    public IReadOnlyList<CollectionNodePoolStatus> GetCollectionNodePoolStatuses()
    {
        lock (_collectionNodeLock)
        {
            return _resourceManager.CollectionNodePools.Values
                .Where(pool => pool.ZoneDefinitionId == DefinitionId)
                .OrderBy(pool => pool.Key)
                .Select(pool =>
                {
                    var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(
                        spawn => spawn.Pool == pool.Key);
                    var activeCount = _npcs.Values.OfType<CollectionNode>().Count(
                        node => node.PoolDefinition.Key == pool.Key);

                    return new CollectionNodePoolStatus(
                        pool.Key,
                        pool.NodeType,
                        activeCount,
                        hardPointCount,
                        pool.GetTargetActiveCount(hardPointCount),
                        pool.RespawnSeconds);
                })
                .ToArray();
        }
    }

    public IReadOnlyList<CollectionNodeSpawnStatus> GetCollectionNodeSpawnStatuses(string? poolKey = null)
    {
        lock (_collectionNodeLock)
        {
            poolKey = poolKey?.Trim().ToLowerInvariant();
            var activeIds = _npcs.Values
                .OfType<CollectionNode>()
                .Select(node => node.SpawnDefinition.Id)
                .ToHashSet();
            var zonePoolKeys = _resourceManager.CollectionNodePools.Values
                .Where(pool => pool.ZoneDefinitionId == DefinitionId)
                .Select(pool => pool.Key)
                .ToHashSet();

            return _resourceManager.CollectionNodeSpawns.Values
                .Where(spawn => zonePoolKeys.Contains(spawn.Pool) &&
                    (poolKey is null || spawn.Pool == poolKey))
                .OrderBy(spawn => spawn.Id)
                .Select(spawn => new CollectionNodeSpawnStatus(
                    spawn.Id, spawn.Pool, spawn.SpawnPosition, activeIds.Contains(spawn.Id)))
                .ToArray();
        }
    }

    public bool TryPlaceCollectionNodeSpawn(string poolKey, Vector4 position, float heading,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition spawn, out bool activated)
    {
        lock (_collectionNodeLock)
        {
            spawn = null;
            activated = false;

            if (string.IsNullOrWhiteSpace(poolKey) ||
                !_resourceManager.CollectionNodePools.TryGetValue(poolKey.Trim().ToLowerInvariant(), out var poolDefinition) ||
                poolDefinition.ZoneDefinitionId != DefinitionId ||
                !_resourceManager.CollectionNodeTypes.TryGetValue(poolDefinition.NodeType, out var typeDefinition))
            {
                return false;
            }

            position.Y += typeDefinition.PlacementYOffset;

            if (!_resourceManager.CollectionNodeSpawns.TryAddPersistent(
                poolDefinition.Key, DefinitionId, position, heading, out spawn))
            {
                return false;
            }

            activated = TryActivateCollectionNodeSpawn(spawn, out _);
            return true;
        }
    }

    public bool TryConfigureCollectionNodePool(string poolKey, int maxActiveNodes, int respawnSeconds,
        out int activeCount, out int targetActiveCount)
    {
        lock (_collectionNodeLock)
        {
            activeCount = 0;
            targetActiveCount = 0;

            if (string.IsNullOrWhiteSpace(poolKey) ||
                !_resourceManager.CollectionNodePools.TryGetValue(poolKey.Trim().ToLowerInvariant(), out var poolDefinition) ||
                poolDefinition.ZoneDefinitionId != DefinitionId ||
                !_resourceManager.CollectionNodePools.TryUpdatePersistent(
                    poolDefinition.Key, maxActiveNodes, respawnSeconds))
            {
                return false;
            }

            activeCount = ReconcileCollectionNodePool(poolDefinition.Key);
            var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(
                spawn => spawn.Pool == poolDefinition.Key);
            targetActiveCount = poolDefinition.GetTargetActiveCount(hardPointCount);
            return true;
        }
    }

    public bool TryRemoveCollectionNodeSpawn(int id,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition removedSpawn)
    {
        lock (_collectionNodeLock)
        {
            removedSpawn = null;

            if (!_resourceManager.CollectionNodeSpawns.TryGetValue(id, out var spawn) ||
                !_resourceManager.CollectionNodePools.TryGetValue(spawn.Pool, out var poolDefinition) ||
                poolDefinition.ZoneDefinitionId != DefinitionId ||
                !_resourceManager.CollectionNodeSpawns.TryRemovePersistent(id))
            {
                return false;
            }

            var activeNode = _npcs.Values
                .OfType<CollectionNode>()
                .FirstOrDefault(node => node.SpawnDefinition.Id == id);

            activeNode?.Dispose();
            ReconcileCollectionNodePool(spawn.Pool);
            removedSpawn = spawn;
            return true;
        }
    }

    public bool TryRemoveNearestCollectionNodeSpawn(Vector4 position, float radius,
        [MaybeNullWhen(false)] out CollectionNodeSpawnDefinition removedSpawn)
    {
        lock (_collectionNodeLock)
        {
            removedSpawn = null;

            if (radius <= 0)
                return false;

            var position3 = new Vector3(position.X, position.Y, position.Z);
            var node = _npcs.Values
                .OfType<CollectionNode>()
                .Where(candidate => _resourceManager.CollectionNodeSpawns.ContainsKey(candidate.SpawnDefinition.Id))
                .Select(candidate => new
                {
                    Node = candidate,
                    DistanceSquared = Vector3.DistanceSquared(
                        new Vector3(candidate.Position.X, candidate.Position.Y, candidate.Position.Z), position3)
                })
                .Where(candidate => candidate.DistanceSquared <= radius * radius)
                .OrderBy(candidate => candidate.DistanceSquared)
                .Select(candidate => candidate.Node)
                .FirstOrDefault();

            return node is not null && TryRemoveCollectionNodeSpawn(node.SpawnDefinition.Id, out removedSpawn);
        }
    }

    private bool TryActivateCollectionNodeSpawn(CollectionNodeSpawnDefinition spawnDefinition,
        [MaybeNullWhen(false)] out CollectionNode node)
    {
        node = null;

        if (!_resourceManager.CollectionNodeSpawns.ContainsKey(spawnDefinition.Id) ||
            !_resourceManager.CollectionNodePools.TryGetValue(spawnDefinition.Pool, out var poolDefinition) ||
            poolDefinition.ZoneDefinitionId != DefinitionId ||
            !_resourceManager.CollectionNodeTypes.TryGetValue(poolDefinition.NodeType, out var typeDefinition) ||
            _npcs.Values.OfType<CollectionNode>().Any(active => active.SpawnDefinition.Id == spawnDefinition.Id))
        {
            return false;
        }

        var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(spawn => spawn.Pool == poolDefinition.Key);
        var activeCount = _npcs.Values.OfType<CollectionNode>().Count(active => active.PoolDefinition.Key == poolDefinition.Key);

        if (activeCount >= poolDefinition.GetTargetActiveCount(hardPointCount))
            return false;

        return TryCreateCollectionNode(typeDefinition, poolDefinition, spawnDefinition, out node);
    }

    protected int ActivateCollectionNodePools()
    {
        var activated = 0;
        var pools = _resourceManager.CollectionNodePools.Values
            .Where(pool => pool.ZoneDefinitionId == DefinitionId)
            .ToArray();

        foreach (var pool in pools)
            activated += RefillCollectionNodePool(pool, int.MaxValue);

        _logger.LogInformation("Activated {count} collection node(s) across {poolCount} pool(s).",
            activated, pools.Length);

        return activated;
    }

    private int ReconcileCollectionNodePool(string poolKey)
    {
        if (!_resourceManager.CollectionNodePools.TryGetValue(poolKey, out var poolDefinition))
            return 0;

        lock (_collectionNodeLock)
        {
            var activeNodes = _npcs.Values
                .OfType<CollectionNode>()
                .Where(node => node.PoolDefinition.Key == poolDefinition.Key)
                .ToList();
            var hardPointCount = _resourceManager.CollectionNodeSpawns.Values.Count(spawn => spawn.Pool == poolDefinition.Key);
            var targetActiveCount = poolDefinition.GetTargetActiveCount(hardPointCount);

            while (activeNodes.Count > targetActiveCount)
            {
                var index = Random.Shared.Next(activeNodes.Count);
                activeNodes[index].Dispose();
                activeNodes.RemoveAt(index);
            }

            RefillCollectionNodePool(poolDefinition, int.MaxValue);
            return _npcs.Values.OfType<CollectionNode>().Count(node => node.PoolDefinition.Key == poolDefinition.Key);
        }
    }

    public void CompleteCollectionNode(CollectionNode node)
    {
        lock (_collectionNodeLock)
        {
            if (!_npcs.ContainsKey(node.Guid))
                return;

            node.DisposeAfterCollection();

            if (!_resourceManager.CollectionNodePools.TryGetValue(node.PoolDefinition.Key, out var poolDefinition) ||
                poolDefinition.ZoneDefinitionId != DefinitionId)
            {
                return;
            }

            var dueTimestamp = Stopwatch.GetTimestamp() +
                (long)(poolDefinition.RespawnSeconds * (double)Stopwatch.Frequency);
            _collectionNodeRefills.Enqueue(
                new CollectionNodePoolRefill(poolDefinition.Key, node.SpawnDefinition.Id), dueTimestamp);
        }
    }

    private bool TryCreateCollectionNode(CollectionNodeTypeDefinition typeDefinition,
        CollectionNodePoolDefinition poolDefinition, CollectionNodeSpawnDefinition spawnDefinition,
        [MaybeNullWhen(false)] out CollectionNode node)
    {
        node = new CollectionNode(this, typeDefinition, poolDefinition, spawnDefinition)
        {
            Guid = GetNpcGuid(null),
            Name = typeDefinition.Name,
            ModelId = typeDefinition.ModelId,
            Scale = typeDefinition.Scale,
            CompositeEffectId = typeDefinition.CompositeEffectId,
            InteractRange = typeDefinition.InteractRange,
            CursorId = typeDefinition.CursorId,
            Visible = true
        };

        if (!TryRegisterEntity(_npcs, node))
        {
            node = null;
            return false;
        }

        node.UpdatePosition(spawnDefinition.SpawnPosition, spawnDefinition.SpawnRotation);
        return true;
    }

    private int RefillCollectionNodePool(CollectionNodePoolDefinition poolDefinition, int maximumToActivate,
        int? avoidHardPointId = null)
    {
        lock (_collectionNodeLock)
        {
            if (_cancellationTokenSource.IsCancellationRequested || poolDefinition.ZoneDefinitionId != DefinitionId ||
                !_resourceManager.CollectionNodeTypes.TryGetValue(poolDefinition.NodeType, out var typeDefinition))
            {
                return 0;
            }

            var activeNodes = _npcs.Values
                .OfType<CollectionNode>()
                .Where(node => node.PoolDefinition.Key == poolDefinition.Key)
                .ToArray();
            var activeHardPointIds = activeNodes
                .Select(node => node.SpawnDefinition.Id)
                .ToHashSet();
            var selected = poolDefinition.SelectSpawnsToActivate(
                _resourceManager.CollectionNodeSpawns.Values, activeHardPointIds, maximumToActivate, avoidHardPointId);
            var activated = 0;

            foreach (var spawn in selected)
            {
                if (TryCreateCollectionNode(typeDefinition, poolDefinition, spawn, out _))
                    activated++;
            }

            return activated;
        }
    }

    private void ProcessCollectionNodeRefills()
    {
        lock (_collectionNodeLock)
        {
            var now = Stopwatch.GetTimestamp();

            while (_collectionNodeRefills.TryPeek(out var refill, out var dueTimestamp) && dueTimestamp <= now)
            {
                _collectionNodeRefills.Dequeue();

                if (_resourceManager.CollectionNodePools.TryGetValue(refill.PoolKey, out var poolDefinition))
                    RefillCollectionNodePool(poolDefinition, 1, refill.CollectedHardPointId);
            }
        }
    }

    public bool TryCreateMount(Player rider, MountDefinition definition, [MaybeNullWhen(false)] out Mount mount)
    {
        mount = new Mount(this, rider, definition)
        {
            Guid = GetNpcGuid(null)
        };

        if (!TryRegisterEntity(_npcs, mount))
        {
            mount = null;
            return false;
        }

        return true;
    }

    public bool TryCreatePlayer(ulong guid, UdpConnection connection, [MaybeNullWhen(false)] out Player player)
    {
        player = new Player(this, connection, _resourceManager, _zoneManager)
        {
            Guid = guid
        };

        if (!TryRegisterEntity(_players, player))
        {
            player = null;
            return false;
        }

        return true;
    }

    public bool TryRemoveNpc(ulong guid)
    {
        return _npcs.TryRemove(guid, out _) && _entities.TryRemove(guid, out _);
    }

    public bool TryRemovePlayer(ulong guid)
    {
        return _players.TryRemove(guid, out _) && _entities.TryRemove(guid, out _);
    }

    #endregion

    #region Zone System

    private Dictionary<int, ZoneTile> GenerateTiles()
    {
        var tiles = new Dictionary<int, ZoneTile>();

        // Generate all tiles
        for (var longitude = _zoneDefinition.StartLongitude; longitude < _zoneDefinition.EndLongitude; longitude++)
        {
            for (var latitude = _zoneDefinition.StartLatitude; latitude < _zoneDefinition.EndLatitude; latitude++)
            {
                var tileHash = ZoneTile.GetHash(longitude, latitude);

                tiles.Add(tileHash, new ZoneTile(longitude, latitude));
            }
        }

        // Calcualte visible tiles
        for (var rootLongitude = _zoneDefinition.StartLongitude; rootLongitude < _zoneDefinition.EndLongitude; rootLongitude++)
        {
            for (var rootLatitude = _zoneDefinition.StartLatitude; rootLatitude < _zoneDefinition.EndLatitude; rootLatitude++)
            {
                var rootTileHash = ZoneTile.GetHash(rootLongitude, rootLatitude);

                var rootTile = tiles[rootTileHash];

                for (var visibleLongitude = rootTile.Longitude - VisibleTileRadius; visibleLongitude <= rootTile.Longitude + VisibleTileRadius; visibleLongitude++)
                {
                    for (var visibleLatitude = rootTile.Latitude - VisibleTileRadius; visibleLatitude <= rootTile.Latitude + VisibleTileRadius; visibleLatitude++)
                    {
                        var visibleTileHash = ZoneTile.GetHash(visibleLongitude, visibleLatitude);

                        if (tiles.TryGetValue(visibleTileHash, out var visibleTile))
                            rootTile.VisibleTiles.Add(visibleTile);
                    }
                }
            }
        }

        return tiles;
    }

    public ZoneTile GetTileFromPosition(Vector4 position)
    {
        var tileLatitude = (int)Math.Floor(position.X / _zoneDefinition.TileSize);
        var tileLongitude = (int)Math.Floor(position.Z / _zoneDefinition.TileSize);

        return GetTileFromCoordinate(tileLongitude, tileLatitude);
    }

    private ZoneTile GetTileFromCoordinate(int longitude, int latitude)
    {
        if (longitude < _zoneDefinition.StartLongitude ||
            longitude >= _zoneDefinition.EndLongitude)
            return ZoneTile.Empty;

        if (latitude < _zoneDefinition.StartLatitude ||
            latitude >= _zoneDefinition.EndLatitude)
            return ZoneTile.Empty;

        var tileHash = ZoneTile.GetHash(longitude, latitude);

        if (!_tiles.TryGetValue(tileHash, out var zoneTile))
            return ZoneTile.Empty;

        return zoneTile;
    }

    public void UpdateEntityZoneTile(IEntity entity, ZoneTile from, ZoneTile to)
    {
        from.Entities.TryRemove(entity.Guid, out _);

        var oldVisibleTiles = from.VisibleTiles;
        var newVisibleTiles = to.VisibleTiles;

        var tilesToAdd = newVisibleTiles.Except(oldVisibleTiles);
        var tilesToRemove = oldVisibleTiles.Except(newVisibleTiles);

        AddEntityToZoneTiles(entity, tilesToAdd);
        RemoveEntityFromZoneTiles(entity, tilesToRemove);

        to.Entities.TryAdd(entity.Guid, entity);
    }

    private void AddEntityToZoneTiles(IEntity entity, IEnumerable<ZoneTile> zoneTiles)
    {
        var npcsToAdd = new List<Npc>();
        var playersToAdd = new List<Player>();

        foreach (var zoneTile in zoneTiles)
        {
            foreach (var zoneTileEntity in zoneTile.Entities)
            {
                if (!zoneTileEntity.Value.Visible || entity == zoneTileEntity.Value)
                    continue;

                switch (zoneTileEntity.Value)
                {
                    case Npc zoneTileNpc:
                        {
                            npcsToAdd.Add(zoneTileNpc);

                            if (entity.Visible)
                            {
                                switch (entity)
                                {
                                    case Npc npc:
                                        break;

                                    case Player player:
                                        zoneTileNpc.OnAddVisiblePlayers(player);
                                        break;
                                }
                            }
                        }
                        break;

                    case Player zoneTilePlayer:
                        {
                            playersToAdd.Add(zoneTilePlayer);

                            if (entity.Visible)
                            {
                                switch (entity)
                                {
                                    case Npc npc:
                                        {
                                            zoneTilePlayer.OnAddVisibleNpcs(npc);
                                        }
                                        break;

                                    case Player player:
                                        zoneTilePlayer.OnAddVisiblePlayers(player);
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
        }

        entity.OnAddVisibleNpcs(npcsToAdd);
        entity.OnAddVisiblePlayers(playersToAdd);
    }

    private void RemoveEntityFromZoneTiles(IEntity entity, IEnumerable<ZoneTile> zoneTiles)
    {
        var npcsToRemove = new List<Npc>();
        var playersToRemove = new List<Player>();

        foreach (var zoneTile in zoneTiles)
        {
            foreach (var zoneTileEntity in zoneTile.Entities)
            {
                if (!zoneTileEntity.Value.Visible || entity == zoneTileEntity.Value)
                    continue;

                switch (zoneTileEntity.Value)
                {
                    case Npc zoneTileNpc:
                        {
                            npcsToRemove.Add(zoneTileNpc);

                            if (entity.Visible)
                            {
                                switch (entity)
                                {
                                    case Npc npc:
                                        break;

                                    case Player player:
                                        zoneTileNpc.OnRemoveVisiblePlayers(player);
                                        break;
                                }
                            }
                        }
                        break;

                    case Player zoneTilePlayer:
                        {
                            playersToRemove.Add(zoneTilePlayer);

                            if (entity.Visible)
                            {
                                switch (entity)
                                {
                                    case Npc npc:
                                        {
                                            if (zoneTilePlayer.Mount is not null && zoneTilePlayer.Mount == npc)
                                                continue;

                                            zoneTilePlayer.OnRemoveVisibleNpcs(npc);
                                        }
                                        break;

                                    case Player player:
                                        zoneTilePlayer.OnRemoveVisiblePlayers(player);
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
        }

        entity.OnRemoveVisibleNpcs(npcsToRemove);
        entity.OnRemoveVisiblePlayers(playersToRemove);
    }

    #endregion

    #region Update

    private async Task UpdateEveryTickAsync()
    {
        while (await _updateEveryTickTimer.WaitForNextTickAsync() && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                ProcessCollectionNodeRefills();

                foreach (var entity in _entities)
                {
                    entity.Value.UpdateEveryTick();
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{Name} ({Id}) - Zone Exception");
            }
        }
    }

    private async Task UpdateEverySecondAsync()
    {
        while (await _updateEverySecondTimer.WaitForNextTickAsync() && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                foreach (var entity in _entities)
                {
                    entity.Value.UpdateEverySecond();
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"{Name} ({Id}) - Zone Exception");
            }
        }
    }

    #endregion

    private ulong GetNpcGuid(ulong? guid)
    {
        lock (_npcGuidLock)
        {
            if (guid.HasValue)
            {
                _nextNpcGuid = Math.Max(_nextNpcGuid, guid.Value + 1);
                return guid.Value;
            }

            return _nextNpcGuid++;
        }
    }

    public void Dispose()
    {
        // NOTE: We currently do not have a grace-period or timeout for this. This
        // function WILL get called if no players exist in a zone (except for FabledRealms)
        // which is the 'Starting Zone'. If things slow down, then this might get called
        // before a player makes it in.
        _zoneManager.RemoveZoneInstance(this);

        _cancellationTokenSource.Cancel();

        lock (_collectionNodeLock)
            _collectionNodeRefills.Clear();

        _tiles.Clear();

        _npcs.Clear();
        _players.Clear();

        _scriptManager.DeleteContext(this);
    }

}
