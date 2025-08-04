using System;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketClientIsReadyHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketClientIsReadyHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(PacketClientIsReady));

        SendQuickChatData(connection);

        SendPointOfInterests(connection);

        SendUpdateStat(connection);

        SendReferenceData(connection);

        SendCoinStoreItemList(connection);

        SendAdventurersJournalInfo(connection);

        SendWelcomeInfo(connection);

        var packetZoneDoneSendingInitialData = new PacketZoneDoneSendingInitialData();

        connection.SendTunneled(packetZoneDoneSendingInitialData);

        var clientUpdatePacketDoneSendingPreloadCharacters = new ClientUpdatePacketDoneSendingPreloadCharacters();

        connection.SendTunneled(clientUpdatePacketDoneSendingPreloadCharacters);

        return true;
    }

    private static void SendQuickChatData(GatewayConnection connection)
    {
        var quickChatSendDataPacket = new QuickChatSendDataPacket();

        quickChatSendDataPacket.QuickChats = _resourceManager.QuickChats.ToDictionary();

        connection.SendTunneled(quickChatSendDataPacket);
    }

    private static void SendPointOfInterests(GatewayConnection connection)
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

        connection.SendTunneled(packetPointOfInterestDefinitionReply);
    }

    private static void SendUpdateStat(GatewayConnection connection)
    {
        var clientUpdatePacketUpdateStat = new ClientUpdatePacketUpdateStat();

        clientUpdatePacketUpdateStat.Guid = connection.Player.Guid;

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

        connection.SendTunneled(clientUpdatePacketUpdateStat);
    }

    private static void SendReferenceData(GatewayConnection connection)
    {
        var referenceDataPacketItemClassDefinitions = new ReferenceDataPacketItemClassDefinitions();

        referenceDataPacketItemClassDefinitions.ItemClasses = _resourceManager.ItemClasses.ToDictionary();

        connection.SendTunneled(referenceDataPacketItemClassDefinitions);

        var referenceDataPacketItemCategoryDefinitions = new ReferenceDataPacketItemCategoryDefinitions();

        referenceDataPacketItemCategoryDefinitions.ItemCategories = _resourceManager.ItemCategories.ToDictionary();
        referenceDataPacketItemCategoryDefinitions.ItemCategoryGroups = _resourceManager.ItemCategoryGroups.ToDictionary();

        connection.SendTunneled(referenceDataPacketItemCategoryDefinitions);

        var referenceDataPacketClientProfileData = new ReferenceDataPacketClientProfileData();

        referenceDataPacketClientProfileData.Profiles = _resourceManager.Profiles.ToDictionary();

        connection.SendTunneled(referenceDataPacketClientProfileData);
    }

    private static void SendCoinStoreItemList(GatewayConnection connection)
    {
        var coinStoreItemListPacket = new CoinStoreItemListPacket();

        var clientItemDefinitions = _resourceManager.ClientItemDefinitions.Values.Where(x => x.Cost > 0 && x.GenderUsage == 0 || x.GenderUsage == connection.Player.Gender);

        foreach (var clientItemDefinition in clientItemDefinitions)
        {
            coinStoreItemListPacket.StaticItems.Add(clientItemDefinition.Id, new ItemDefinitionMetaData
            {
                Id = clientItemDefinition.Id,
                CategoryId = clientItemDefinition.CategoryId
            });
        }

        connection.SendTunneled(coinStoreItemListPacket);
    }
    private static void SendAdventurersJournalInfo(GatewayConnection connection)
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

        connection.SendTunneled(adventurersJournal);
    }

    private static void SendWelcomeInfo(GatewayConnection connection)
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

        connection.SendTunneled(packetLoadWelcomeScreen);
    }
}