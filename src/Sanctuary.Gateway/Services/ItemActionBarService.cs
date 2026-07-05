using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Services;

public static class ItemActionBarService
{
    public const int ActionBarId = 2;
    public const int SlotsPerPage = 4;
    public const int PageCount = 9;
    public const int SlotCount = SlotsPerPage * PageCount;

    private const int CarouselAliasItemGuidBase = 1_500_000_000;
    private const int DefaultCooldownMs = 1000;
    private static readonly TimeSpan BoomboxLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BoomboxCooldown = TimeSpan.FromMinutes(2);
    private const float BoomboxSpawnMinRadius = 1.5f;
    private const float BoomboxSpawnMaxRadius = 5.0f;
    private const float BoomboxSpawnMinSpacing = 1.25f;
    private const float BoomboxDanceMaxDistance = 12.0f;
    private const int DefaultBoomboxObjectAnimationId = 2100; // amb_loop_01
    private static readonly ConcurrentDictionary<(ulong PlayerGuid, int DefinitionId), ActiveBoomboxState> ActiveBoomboxes = new();
    private static readonly ConcurrentDictionary<(ulong PlayerGuid, int DefinitionId), DateTime> LastBoomboxSpawnByPlayerAndDefinition = new();
    private static readonly BoomboxPresentation DefaultBoomboxPresentation = new(0, [], []);

    private static readonly IReadOnlyDictionary<int, BoomboxDefinitionFallback> BoomboxDefinitionFallbacks =
        new Dictionary<int, BoomboxDefinitionFallback>
        {
            [35759] = new("mkt_boombox_dwarftech_01.adr", "boombox-dwarftech-M", "clockwork"),
            [48244] = new("bw_boombox_01.adr", "boombox-bw-M", "bw_boombox"),
            [56774] = new("mkt_boombox_ghettoblaster_01.adr", "boombox-ghettoblaster-M", "hiphop"),
            [69825] = new("mkt_boombox_realmshake_01.adr", "boombox-realmshake-M", "chiller"),
            [76877] = new("mkt_boombox_chicken_01.adr", string.Empty, "chicken"),
            [76927] = new("mkt_boombox_kpop_01.adr", "", "popdance"),
            [76930] = new("mkt_boombox_bigband_01.adr", "boombox-bigband-M", "bigband"),
            [76933] = new("mkt_boombox_western_01.adr", "boombox-western-M", "hootenanny"),
            [76934] = new("mkt_boombox_xylophone_01.adr", "boombox-xylophone-M", "xylophone"),
            [78075] = new("mkt_boombox_rainbowleaf_01.adr", string.Empty, "plants")
        };

    private static readonly (string Token, BoomboxPresentation Presentation)[] BoomboxPresentations =
    [
        new("xmas", new(0, ["mkt_xmas_boombox_01_amb_bump_and_jump_loop.gr2", "mkt_xmas_boombox_01_amb_bump_loop.gr2"], [3531, 3532, 3533, 3534])),
        new("jinglebell", new(0, [], [3531, 3532, 3533, 3534])),
        new("valentines", new(0, [], [3560, 3561, 3562, 3563])),
        new("lovelectric", new(0, [], [3560, 3561, 3562, 3563])),
        new("8bit", new(0, ["mkt_boombox_8bit_amb_loop_01.gr2"], [3568011, 3569011, 3570011, 3571011])),
        new("clockwork", new(0, [], [3568011, 3569011, 3570011, 3571011])),
        new("rainbow", new(0, [], [3501, 3502, 3503, 3504, 3505])),
        new("rainbowleaf", new(0, ["mkt_boombox_rainbowleaf_01_amb_loop.gr2"], [3564, 3565, 3566, 3567])),
        new("ghettoblaster", new(0, ["mkt_boombox_ghettoblaster_01_amb_bump_and_jump_loop.gr2", "mkt_boombox_ghettoblaster_01_amb_bump_loop.gr2"], [3501, 3502, 3503, 3504, 3505])),
        new("realmsroll", new(0, ["mkt_boombox_realmsroll_01_amb_bump_loop.gr2"], [3541, 3542, 3543, 3544])),
        new("tiki", new(0, ["mkt_boombox_tiki_01_amb_looping_01.gr2"], [3568006, 3569006, 3570006, 3571006])),
        new("totem", new(0, ["mkt_boombox_totem_01_amb_bump_and_jump_loop.gr2"], [3551, 3552, 3553, 3554])),
        new("chicken", new(0, ["mkt_boombox_chicken_01_amb_bump_jump_loop.gr2", "mkt_boombox_chicken_01_amb_bump_loop.gr2"], [35010, 35020, 35030, 35040, 35050])),
        new("robo", new(0, ["boombox_robo_loc_amb_looping.gr2"], [3568005, 3569005, 3570005, 3571005])),
        new("dwarftech", new(0, [], [3568011, 3569011, 3570011, 3571011])),
        new("kpop", new(0, ["mkt_boombox_kpop_amb_bump_loop_01.gr2"], [3568010, 3569010, 3570010, 3571010])),
        new("popdance", new(0, ["mkt_boombox_kpop_amb_bump_loop_01.gr2"], [3568010, 3569010, 3570010, 3571010])),
        new("bw_boombox", new(0, ["bw_boombox_01_amb_bump_and_jump_loop.gr2", "bw_boombox_01_amb_bump_loop.gr2"], [3501, 3502, 3503, 3504, 3505])),
        new("ballet", new(0, ["mkt_boombox_ballet_01_amb_bump_loop.gr2"], [3568000, 3569000, 3570000, 3571000])),
        new("bigband", new(0, ["boombox_bigband_amb_oneshot_01.gr2"], [3568001, 3569001, 3570001, 3571001])),
        new("gramophone", new(0, [], [3568002, 3569002, 3570002, 3571002])),
        new("headbanger", new(0, ["mkt_boombox_headbanger_01_amb_looping.gr2"], [3568003, 3569003, 3570003, 3571003])),
        new("heavymetal", new(0, [], [3568004, 3569004, 3570004, 3571004])),
        new("wakyscifi", new(0, ["boombox_wakyscifi_amb_looping.gr2"], [3568007, 3569007, 3570007, 3571007])),
        new("wackyscifi", new(0, ["boombox_wakyscifi_amb_looping.gr2"], [3568007, 3569007, 3570007, 3571007])),
        new("western", new(0, ["mkt_boombox_western_01_amb_looping_01.gr2"], [3568008, 3569008, 3570008, 3571008])),
        new("hootenanny", new(0, [], [3568008, 3569008, 3570008, 3571008])),
        new("xylophone", new(0, ["boombox_xylophone_amb_looping.gr2"], [3568009, 3569009, 3570009, 3571009])),
        new("shuffle", new(0, [], [3568012, 3569012, 3570012, 3571012, 3572012])),
        new("chiller", new(0, [], [3511, 3512, 3513, 3514, 3515])),
        new("zombie", new(0, [], [3511, 3512, 3513, 3514, 3515])),
        new("thriller", new(0, [], [3511, 3512, 3513, 3514, 3515])),
        new("hiphop", new(0, ["mkt_boombox_ghettoblaster_01_amb_bump_and_jump_loop.gr2", "mkt_boombox_ghettoblaster_01_amb_bump_loop.gr2"], [3501, 3502, 3503, 3504, 3505])),
        new("freestyle", new(0, [], [3501, 3502, 3503, 3504, 3505])),
        new("realmshake", new(0, [], [3501, 3502, 3503, 3504, 3505])),
        new("plants", new(0, [], [3564, 3565, 3566, 3567]))
    ];


    public static ActionBarSlot CreateSelectorSlot()
    {
        // Legacy name. Do not create fake non-empty selector slots.
        // Fake selector slots populate the carousel with blank occupied entries.
        return CreateEmptySlot();
    }

    public static ActionBarSlot CreateEmptySlot()
    {
        return new ActionBarSlot
        {
            IsEmpty = true,

            IconId = 0,
            IconTintId = 0,
            NameId = 0,

            Unknown5 = 0,
            Unknown6 = 0,
            Unknown7 = 0,

            ManaCost = 0,
            Enabled = false,

            Unknown10 = 0,
            TotalRefreshTime = 0,
            Unknown12 = 0,

            Quantity = 0,

            ForceDismount = false,
            Unknown15 = 0
        };
    }

    public static void RefreshQuickItemCarouselState(GatewayConnection connection, ILogger logger)
    {

        connection.SendTunneled(new ExecuteScriptPacket
        {
            Script = "GameDock.refreshConsumables"
        });

        SendGameDockConsumableEvent(connection, "refreshConsumables");

        connection.SendTunneled(new ExecuteScriptPacket
        {
            Script = "QuickItem.Populate"
        });

        connection.SendTunneled(new ExecuteScriptPacket
        {
            Script = "QuickItem.PopulateSelectors"
        });

        logger.LogInformation(
            "{connection} refreshed quick-item carousel UI state after owned item replay.",
            connection);
    }

    public static bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < SlotCount;
    }

    public static ActionBarSlot CreateItemSlot(ClientItem item, ClientItemDefinition definition)
    {
        return new ActionBarSlot
        {
            IsEmpty = false,
            IconId = definition.Icon.Id,
            NameId = definition.NameId,
            Unknown5 = 1,
            Unknown6 = 4,
            Unknown7 = 15,
            Enabled = true,
            Unknown10 = DefaultCooldownMs,
            TotalRefreshTime = DefaultCooldownMs,
            Quantity = item.Count,
            ForceDismount = true,
            Unknown15 = DefaultCooldownMs
        };
    }

    public static void ApplyActionBarItemCapabilities(ClientItem item, ClientItemDefinition definition)
    {
        if (!IsValidActionBarItem(definition))
            return;

        item.Count = Math.Max(item.Count, 1);
        item.ConsumedCount = 0;
        item.ActivateEnabled = true;
        item.AbilityCount = Math.Max(item.AbilityCount, item.Count);
    }

    public static void ApplyCarouselDefinitionCompatibility(IResourceManager resourceManager)
    {
        AddRecoveredBoomboxDefinitions(resourceManager);

        foreach (var definition in resourceManager.ClientItemDefinitions.Values)
        {
            if (!IsBoomboxDefinition(definition))
                continue;

            definition.SingleUse = true;
            definition.MaxStackSize = Math.Max(definition.MaxStackSize, 1);

            if (definition.CategoryId == 3)
                definition.CategoryId = 9;

            if (definition.Class == 171)
                definition.Class = 9;
        }
    }

    private static void AddRecoveredBoomboxDefinitions(IResourceManager resourceManager)
    {
        AddRecoveredBoomboxDefinition(
            resourceManager,
            id: 76877,
            nameId: 29788,
            descriptionId: 29793,
            iconId: 6822,
            activatableAbilityId: 4931,
            modelName: "mkt_boombox_chicken_01.adr",
            textureAlias: string.Empty);

        AddRecoveredBoomboxDefinition(
            resourceManager,
            id: 78075,
            nameId: 435706,
            descriptionId: 435707,
            iconId: 7488,
            activatableAbilityId: 5037,
            modelName: "mkt_boombox_rainbowleaf_01.adr",
            textureAlias: string.Empty);
    }

    private static void AddRecoveredBoomboxDefinition(
        IResourceManager resourceManager,
        int id,
        int nameId,
        int descriptionId,
        int iconId,
        int activatableAbilityId,
        string modelName,
        string textureAlias)
    {
        if (resourceManager.ClientItemDefinitions.ContainsKey(id))
            return;

        resourceManager.ClientItemDefinitions.TryAdd(id, new ClientItemDefinition
        {
            Id = id,
            Type = 1,
            NameId = nameId,
            DescriptionId = descriptionId,
            Icon = new IconData { Id = iconId },
            Class = 9,
            MaxStackSize = 1,
            NoTrade = true,
            SingleUse = true,
            MinProfileRank = 1,
            TintAlias = "dyetint",
            TextureAlias = textureAlias,
            MemberDiscount = 10,
            ModelName = modelName,
            CategoryId = 9,
            ActivatableAbilityId = activatableAbilityId,
            NoSale = true,
            NonMiniGame = true,
            Rarity = 3
        });
    }

    public static ClientUpdatePacketUpdateActionBarSlot CreateSlotUpdate(int slot, ActionBarSlot actionBarSlot)
    {
        return new ClientUpdatePacketUpdateActionBarSlot
        {
            Data =
            {
                Id = ActionBarId,
                Slot = slot
            },
            Slot = actionBarSlot
        };
    }

    public static int LoadPersistedSlots(
        GatewayConnection connection,
        IResourceManager resourceManager,
        IEnumerable<DbItemActionBarSlot> persistedSlots,
        ILogger logger,
        bool sendUpdates)
    {
        var loadedCount = 0;

        EnsurePlayerActionBar(connection.Player);

        foreach (var persistedSlot in persistedSlots
                     .Where(x => x.ActionBarId == ActionBarId)
                     .OrderBy(x => x.Slot))
        {
            if (!IsValidSlot(persistedSlot.Slot))
            {
                logger.LogWarning(
                    "{connection} skipped persisted item action bar slot outside valid range. ( Slot: {slot}, ItemGuid: {itemGuid} )",
                    connection,
                    persistedSlot.Slot,
                    persistedSlot.ItemId);

                continue;
            }

            var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == persistedSlot.ItemId);

            if (clientItem is null)
            {
                logger.LogWarning(
                    "{connection} skipped persisted item action bar assignment for missing owned item. ( Slot: {slot}, ItemGuid: {itemGuid} )",
                    connection,
                    persistedSlot.Slot,
                    persistedSlot.ItemId);

                continue;
            }

            if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) ||
                !IsValidActionBarItem(definition))
            {
                logger.LogWarning(
                    "{connection} skipped persisted item action bar assignment for invalid item. ( Slot: {slot}, ItemGuid: {itemGuid}, Definition: {definition} )",
                    connection,
                    persistedSlot.Slot,
                    clientItem.Id,
                    clientItem.Definition);

                continue;
            }

            connection.Player.ItemActionBarSlots[persistedSlot.Slot] = clientItem.Id;

            var actionBarSlot = CreateItemSlot(clientItem, definition);
            SetPlayerActionBarSlot(connection.Player, persistedSlot.Slot, actionBarSlot);

            if (sendUpdates)
                connection.SendTunneled(CreateSlotUpdate(persistedSlot.Slot, actionBarSlot));

            loadedCount++;

            logger.LogInformation(
                "{connection} loaded persisted item action bar assignment. ( Slot: {slot}, ItemGuid: {itemGuid}, Definition: {definition}, NameId: {nameId}, ModelName: {modelName}, ActivatableAbilityId: {abilityId} )",
                connection,
                persistedSlot.Slot,
                clientItem.Id,
                clientItem.Definition,
                definition.NameId,
                definition.ModelName,
                definition.ActivatableAbilityId);
        }

        return loadedCount;
    }

    public static bool ClearLegacyAutoSeededBoomboxSlots(
        GatewayConnection connection,
        IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IReadOnlyCollection<DbItemActionBarSlot> persistedSlots,
        ILogger logger)
    {
        return false;
    }

    public static int SeedOwnedBoomboxSlots(
        GatewayConnection connection,
        IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        ILogger logger,
        bool sendUpdates)
    {
        // Do not auto-fill the quick-item action bar with owned boomboxes.
        // The desired behavior is: empty selector slot is clickable -> carousel opens -> player chooses item.
        logger.LogInformation(
            "{connection} skipped owned boombox auto-seeding so empty quick-item slots can open the carousel.",
            connection);

        return 0;
    }

    public static int LoadPersistedSlotsFromDatabase(
        GatewayConnection connection,
        IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        ILogger logger,
        bool sendUpdates)
    {
        try
        {
            var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
            using var dbContext = dbContextFactory.CreateDbContext();
            using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = """
                SELECT Slot, ItemId
                FROM ItemActionBarSlots
                WHERE CharacterId = @characterId AND ActionBarId = @actionBarId
                ORDER BY Slot
                """;

            AddParameter(command, "@characterId", characterId);
            AddParameter(command, "@actionBarId", ActionBarId);

            if (command.Connection!.State != ConnectionState.Open)
                command.Connection.Open();

            EnsureItemActionBarSlotTable(command.Connection);

            var persistedSlots = new List<DbItemActionBarSlot>();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                persistedSlots.Add(new DbItemActionBarSlot
                {
                    CharacterId = characterId,
                    ActionBarId = ActionBarId,
                    Slot = Convert.ToInt32(reader["Slot"]),
                    ItemId = Convert.ToInt32(reader["ItemId"]),
                    ItemCharacterId = characterId
                });
            }

            return LoadPersistedSlots(
                connection,
                resourceManager,
                persistedSlots,
                logger,
                sendUpdates);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{connection} failed to load persisted item action bar slots.",
                connection);

            return 0;
        }
    }

    public static void SendOwnedActionBarItemUpdates(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ILogger logger)
    {
        var sentCount = 0;
        var boomboxCount = 0;

        foreach (var clientItem in connection.Player.Items)
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) ||
                !IsValidActionBarItem(definition))
            {
                continue;
            }

            ApplyActionBarItemCapabilities(clientItem, definition);

            connection.SendTunneled(CreateItemUpdate(clientItem, definition));
            sentCount++;

            if (IsBoomboxDefinition(definition))
                boomboxCount++;
        }

        logger.LogInformation(
            "{connection} refreshed owned quick-item inventory metadata for carousel population. ( Count: {count}, BoomboxCount: {boomboxCount} )",
            connection,
            sentCount,
            boomboxCount);
    }

    public static void SendOwnedActionBarItemDefinitions(
    GatewayConnection connection,
    IResourceManager resourceManager,
    ILogger logger)
    {
        var sentCount = 0;
        var boomboxCount = 0;
        var sentDefinitions = new HashSet<int>();

        foreach (var clientItem in connection.Player.Items.OrderBy(x => x.Definition))
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) ||
                !IsValidActionBarItem(definition))
            {
                continue;
            }

            if (!sentDefinitions.Add(definition.Id))
                continue;

            using var writer = new PacketWriter();

            definition.Serialize(writer);

            connection.SendTunneled(new PlayerUpdatePacketItemDefinitionRequest
            {
                Id = definition.Id,
                Payload = writer.Buffer
            });

            sentCount++;

            if (IsBoomboxDefinition(definition))
                boomboxCount++;

            logger.LogInformation(
                "{connection} sent owned quick-item definition for carousel cache. ( Definition: {definition}, NameId: {nameId}, IconId: {iconId}, CategoryId: {categoryId}, Class: {class}, Type: {type}, ActivatableAbilityId: {abilityId}, ModelName: {modelName}, TextureAlias: {textureAlias} )",
                connection,
                definition.Id,
                definition.NameId,
                definition.Icon.Id,
                definition.CategoryId,
                definition.Class,
                definition.Type,
                definition.ActivatableAbilityId,
                definition.ModelName,
                definition.TextureAlias);
        }

        logger.LogInformation(
            "{connection} sent owned quick-item definitions for carousel cache. ( Count: {count}, BoomboxCount: {boomboxCount} )",
            connection,
            sentCount,
            boomboxCount);
    }

    public static void ReplayOwnedActionBarItemsLikeShopPurchases(
    GatewayConnection connection,
    IResourceManager resourceManager,
    ILogger logger)
    {
        var sentCount = 0;
        var boomboxCount = 0;

        foreach (var clientItem in connection.Player.Items.OrderBy(x => x.Id))
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) ||
                !IsValidActionBarItem(definition))
            {
                continue;
            }

            ApplyActionBarItemCapabilities(clientItem, definition);

            connection.SendTunneled(new ClientUpdatePacketItemDelete
            {
                ItemGuid = clientItem.Id
            });

            connection.SendTunneled(CreateItemAdd(clientItem, definition));
            connection.SendTunneled(CreateItemUpdate(clientItem, definition));

            sentCount++;

            if (IsBoomboxDefinition(definition))
                boomboxCount++;

            logger.LogInformation(
                "{connection} replayed owned quick item like fresh shop purchase. ( ItemGuid: {itemGuid}, Definition: {definition}, Count: {count}, NameId: {nameId}, IconId: {iconId}, ActivatableAbilityId: {abilityId}, IsBoombox: {isBoombox} )",
                connection,
                clientItem.Id,
                clientItem.Definition,
                clientItem.Count,
                definition.NameId,
                definition.Icon.Id,
                definition.ActivatableAbilityId,
                IsBoomboxDefinition(definition));
        }

        logger.LogInformation(
            "{connection} replayed owned quick items like shop purchases for carousel population. ( Count: {count}, BoomboxCount: {boomboxCount} )",
            connection,
            sentCount,
            boomboxCount);
    }

    public static void SendOwnedActionBarItemAdds(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ILogger logger)
    {
        var sentCount = 0;
        var boomboxCount = 0;

        foreach (var clientItem in connection.Player.Items)
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) ||
                !IsValidActionBarItem(definition))
            {
                continue;
            }

            ApplyActionBarItemCapabilities(clientItem, definition);

            connection.SendTunneled(CreateItemAdd(clientItem, definition));
            sentCount++;

            if (IsBoomboxDefinition(definition))
                boomboxCount++;
        }

        logger.LogInformation(
            "{connection} replayed owned quick-item inventory rows for carousel population. ( Count: {count}, BoomboxCount: {boomboxCount} )",
            connection,
            sentCount,
            boomboxCount);
    }

    public static int SendOwnedCarouselAliasItemAdds(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ILogger logger)
    {
        var sentCount = 0;
        var boomboxCount = 0;

        foreach (var clientItem in connection.Player.Items.OrderBy(x => x.Id))
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) ||
                !IsValidActionBarItem(definition))
            {
                continue;
            }

            ApplyActionBarItemCapabilities(clientItem, definition);

            var aliasItemGuid = CreateCarouselAliasItemGuid(clientItem.Id);
            connection.QuickItemCarouselAliases[aliasItemGuid] = clientItem.Id;

            var aliasItem = new ClientItem
            {
                Id = aliasItemGuid,
                Tint = clientItem.Tint,
                Count = clientItem.Count,
                ConsumedCount = clientItem.ConsumedCount,
                LastCastTime = clientItem.LastCastTime,
                AbilityCount = clientItem.AbilityCount,
                Definition = clientItem.Definition,
                ActivateEnabled = clientItem.ActivateEnabled
            };

            connection.SendTunneled(CreateItemAdd(aliasItem, definition));
            connection.SendTunneled(CreateItemUpdate(aliasItem, definition));
            sentCount++;

            if (IsBoomboxDefinition(definition))
                boomboxCount++;
        }

        logger.LogInformation(
            "{connection} replayed owned quick-items as carousel alias rows. ( Count: {count}, BoomboxCount: {boomboxCount} )",
            connection,
            sentCount,
            boomboxCount);

        return sentCount;
    }

    public static void ReplayOwnedCarouselItemsThroughShopCompletion(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ILogger logger)
    {
        SendOwnedActionBarItemDefinitions(connection, resourceManager, logger);

        var sentCount = SendOwnedShopStyleItemUpdates(connection, resourceManager, logger);
        if (sentCount == 0)
        {
            logger.LogInformation(
                "{connection} skipped quick-item carousel shop-completion replay because no owned quick-items were found.",
                connection);

            return;
        }

        SendQuickItemCarouselShopCompletion(connection, sentCount, logger);
        RefreshQuickItemCarouselState(connection, logger);
    }

    public static void ReplayOwnedCarouselItemsForMarketplaceOpen(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ILogger logger)
    {
        SendOwnedActionBarItemDefinitions(connection, resourceManager, logger);

        var sentCount = SendOwnedShopStyleItemUpdates(connection, resourceManager, logger);
        if (sentCount == 0)
        {
            logger.LogInformation(
                "{connection} skipped quick-item carousel marketplace-open replay because no owned quick-items were found.",
                connection);

            return;
        }

        RefreshQuickItemCarouselState(connection, logger);

        logger.LogInformation(
            "{connection} replayed owned quick-items for marketplace-open carousel population without sending purchase completion. ( Count: {count} )",
            connection,
            sentCount);
    }

    private static int SendOwnedShopStyleItemUpdates(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ILogger logger)
    {
        var sentCount = 0;
        var boomboxCount = 0;

        foreach (var clientItem in connection.Player.Items
                     .Where(x => !IsCarouselAliasItemGuid(x.Id))
                     .OrderBy(x => x.Id))
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) ||
                !IsValidActionBarItem(definition))
            {
                continue;
            }

            ApplyActionBarItemCapabilities(clientItem, definition);

            connection.SendTunneled(new ClientUpdatePacketItemUpdate
            {
                ItemGuid = clientItem.Id,
                Count = clientItem.Count
            });

            sentCount++;

            if (IsBoomboxDefinition(definition))
                boomboxCount++;
        }

        logger.LogInformation(
            "{connection} replayed owned quick-items as shop-style real item updates. ( Count: {count}, BoomboxCount: {boomboxCount} )",
            connection,
            sentCount,
            boomboxCount);

        return sentCount;
    }

    private static void SendQuickItemCarouselShopCompletion(
        GatewayConnection connection,
        int itemCount,
        ILogger logger)
    {
        var characterId = (int)GuidHelper.GetPlayerId(connection.Player.Guid);
        var orderTrackingId = 900_000_000 + Math.Abs(characterId % 10_000_000);

        connection.SendTunneled(new PacketInGamePurchasePlaceOrderResponse
        {
            OrderTrackingId = orderTrackingId,
            Result = 1,
            OrderId = $"carousel-{characterId}",
            Discount = 0,
            Total = 0
        });

        logger.LogInformation(
            "{connection} sent shop completion response for owned quick-item carousel replay. ( OrderTrackingId: {orderTrackingId}, Count: {count} )",
            connection,
            orderTrackingId,
            itemCount);
    }

    public static void AddOwnedCarouselAliasesToSelfInventory(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ILogger logger)
    {
        var addedCount = 0;
        var boomboxCount = 0;

        foreach (var clientItem in connection.Player.Items
                     .Where(x => !IsCarouselAliasItemGuid(x.Id))
                     .OrderBy(x => x.Id)
                     .ToList())
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition) ||
                !IsValidActionBarItem(definition))
            {
                continue;
            }

            ApplyActionBarItemCapabilities(clientItem, definition);

            var aliasItemGuid = CreateCarouselAliasItemGuid(clientItem.Id);
            connection.QuickItemCarouselAliases[aliasItemGuid] = clientItem.Id;

            if (connection.Player.Items.Any(x => x.Id == aliasItemGuid))
                continue;

            connection.Player.Items.Add(new ClientItem
            {
                Id = aliasItemGuid,
                Tint = clientItem.Tint,
                Count = clientItem.Count,
                ConsumedCount = clientItem.ConsumedCount,
                LastCastTime = clientItem.LastCastTime,
                AbilityCount = clientItem.AbilityCount,
                Definition = clientItem.Definition,
                ActivateEnabled = clientItem.ActivateEnabled
            });

            addedCount++;

            if (IsBoomboxDefinition(definition))
                boomboxCount++;
        }

        logger.LogInformation(
            "{connection} inserted owned quick-items as carousel alias rows into self inventory payload. ( Count: {count}, BoomboxCount: {boomboxCount} )",
            connection,
            addedCount,
            boomboxCount);
    }

    public static void RemoveCarouselAliasesFromServerInventory(GatewayConnection connection)
    {
        connection.Player.Items.RemoveAll(x => IsCarouselAliasItemGuid(x.Id));
    }

    public static ClientUpdatePacketItemAdd CreateItemAdd(ClientItem item, ClientItemDefinition definition)
    {
        ApplyActionBarItemCapabilities(item, definition);

        using var writer = new PacketWriter();

        item.Serialize(writer);

        return new ClientUpdatePacketItemAdd
        {
            Payload = writer.Buffer
        };
    }

    public static ClientUpdatePacketItemUpdate CreateItemUpdate(ClientItem item, ClientItemDefinition definition)
    {
        ApplyActionBarItemCapabilities(item, definition);

        return new ClientUpdatePacketItemUpdate
        {
            ItemGuid = item.Id,
            Count = item.Count,
            ActionBarId = IsValidActionBarItem(definition) ? ActionBarId : -1,
            ConsumedCount = item.ConsumedCount,
            AbilityCount = item.AbilityCount
        };
    }

    public static void SendCurrentSlots(GatewayConnection connection)
    {
        EnsurePlayerActionBar(connection.Player);

        var actionBar = connection.Player.ActionBars[ActionBarId];

        foreach (var entry in actionBar.Slots.OrderBy(x => x.Key))
        {
            var slot = entry.Key;
            var actionBarSlot = entry.Value;

            // Do not spam blank/empty quick-item slots into the client.
            // That can overwrite/pollute the carousel with empty entries.
            if (actionBarSlot.IsEmpty && !connection.Player.ItemActionBarSlots.ContainsKey(slot))
                continue;

            connection.SendTunneled(CreateSlotUpdate(slot, actionBarSlot));
        }
    }

    public static bool TryAssignItem(
        GatewayConnection connection,
        IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        int slot,
        int itemGuid,
        ILogger logger)
    {
        if (!IsValidSlot(slot))
        {
            logger.LogWarning(
                "{connection} tried to assign item action bar slot outside valid carousel range. ( Slot: {slot}, ItemGuid: {itemGuid}, SlotCount: {slotCount} )",
                connection,
                slot,
                itemGuid,
                SlotCount);

            return true;
        }

        if (itemGuid == 0)
        {
            var hadAssignment = connection.Player.ItemActionBarSlots.TryRemove(slot, out _);
            var selectorSlot = CreateSelectorSlot();
            SetPlayerActionBarSlot(connection.Player, slot, selectorSlot);

            RemovePersistedSlot(connection, dbContextFactory, slot, logger);

            connection.SendTunneled(CreateSlotUpdate(slot, selectorSlot));

            logger.LogInformation(
                "{connection} removed item action bar assignment. ( Slot: {slot}, HadAssignment: {hadAssignment} )",
                connection,
                slot,
                hadAssignment);

            return true;
        }

        var requestedItemGuid = itemGuid;

        if (connection.QuickItemCarouselAliases.TryGetValue(itemGuid, out var realItemGuid))
            itemGuid = realItemGuid;

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == itemGuid);

        if (clientItem is null)
        {
            logger.LogWarning(
                "{connection} tried to assign an item they do not own to the item action bar. ( Slot: {slot}, ItemGuid: {itemGuid}, RequestedItemGuid: {requestedItemGuid} )",
                connection,
                slot,
                itemGuid,
                requestedItemGuid);

            connection.Player.ItemActionBarSlots.TryRemove(slot, out _);

            var selectorSlot = CreateSelectorSlot();
            SetPlayerActionBarSlot(connection.Player, slot, selectorSlot);
            connection.SendTunneled(CreateSlotUpdate(slot, selectorSlot));

            return true;
        }

        if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            logger.LogWarning(
                "{connection} tried to assign an item with an unknown definition to the item action bar. ( Slot: {slot}, ItemGuid: {itemGuid}, Definition: {definition} )",
                connection,
                slot,
                itemGuid,
                clientItem.Definition);

            connection.Player.ItemActionBarSlots.TryRemove(slot, out _);

            var selectorSlot = CreateSelectorSlot();
            SetPlayerActionBarSlot(connection.Player, slot, selectorSlot);
            connection.SendTunneled(CreateSlotUpdate(slot, selectorSlot));

            return true;
        }

        connection.Player.ItemActionBarSlots[slot] = itemGuid;

        var actionBarSlot = CreateItemSlot(clientItem, clientItemDefinition);
        SetPlayerActionBarSlot(connection.Player, slot, actionBarSlot);

        SavePersistedSlot(connection, dbContextFactory, slot, itemGuid, logger);

        logger.LogInformation(
            "{connection} assigned item to action bar. ( Slot: {slot}, ItemGuid: {itemGuid}, RequestedItemGuid: {requestedItemGuid}, Definition: {definition}, NameId: {nameId}, ModelName: {modelName}, ActivatableAbilityId: {abilityId} )",
            connection,
            slot,
            itemGuid,
            requestedItemGuid,
            clientItem.Definition,
            clientItemDefinition.NameId,
            clientItemDefinition.ModelName,
            clientItemDefinition.ActivatableAbilityId);

        connection.SendTunneled(CreateSlotUpdate(slot, actionBarSlot));
        return true;
    }

    public static bool TryAssignItemByRecord(
        GatewayConnection connection,
        IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        int slot,
        ItemRecord itemRecord,
        ILogger logger)
    {
        var clientItem = connection.Player.Items
            .OrderBy(x => x.Id)
            .FirstOrDefault(x => x.Definition == itemRecord.Definition && x.Tint == itemRecord.Tint);

        if (clientItem is null)
        {
            logger.LogWarning(
                "{connection} tried to assign item record they do not own to the item action bar. ( Slot: {slot}, Definition: {definition}, Tint: {tint} )",
                connection,
                slot,
                itemRecord.Definition,
                itemRecord.Tint);

            return true;
        }

        return TryAssignItem(
            connection,
            resourceManager,
            dbContextFactory,
            slot,
            clientItem.Id,
            logger);
    }

    public static bool TryActivateItemSlot(
        GatewayConnection connection,
        IResourceManager resourceManager,
        int slot,
        ILogger logger)
    {
        if (!IsValidSlot(slot))
        {
            logger.LogWarning(
                "{connection} tried to activate item action bar slot outside valid carousel range. ( Slot: {slot}, SlotCount: {slotCount} )",
                connection,
                slot,
                SlotCount);

            return true;
        }

        if (!connection.Player.ItemActionBarSlots.TryGetValue(slot, out var itemGuid))
        {
            logger.LogInformation(
                "{connection} activated selector item action bar slot; requesting quick-item carousel. ( Slot: {slot} )",
                connection,
                slot);

            OpenQuickItemCarousel(connection, resourceManager, slot, logger);
            return true;
        }

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == itemGuid);
        if (clientItem is null)
        {
            logger.LogWarning(
                "{connection} activated item action bar slot with missing owned item. ( Slot: {slot}, ItemGuid: {itemGuid} )",
                connection,
                slot,
                itemGuid);

            connection.Player.ItemActionBarSlots.TryRemove(slot, out _);

            var selectorSlot = CreateSelectorSlot();
            SetPlayerActionBarSlot(connection.Player, slot, selectorSlot);
            connection.SendTunneled(CreateSlotUpdate(slot, selectorSlot));

            return true;
        }

        if (!resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var definition))
        {
            logger.LogWarning(
                "{connection} activated item action bar slot with unknown definition. ( Slot: {slot}, ItemGuid: {itemGuid}, Definition: {definition} )",
                connection,
                slot,
                itemGuid,
                clientItem.Definition);

            return true;
        }

        if (TryActivateBoombox(connection, resourceManager, clientItem, definition, logger))
            return true;

        logger.LogInformation(
            "{connection} activated quick item with no server-side item handler. ( Slot: {slot}, ItemGuid: {itemGuid}, Definition: {definition}, ActivatableAbilityId: {abilityId} )",
            connection,
            slot,
            itemGuid,
            clientItem.Definition,
            definition.ActivatableAbilityId);

        return true;
    }

    private static void OpenQuickItemCarousel(
        GatewayConnection connection,
        IResourceManager resourceManager,
        int slot,
        ILogger logger)
    {
        ReplayOwnedCarouselItemsThroughShopCompletion(connection, resourceManager, logger);

        connection.SendTunneled(new ExecuteScriptPacket
        {
            Script = "QuickItem.Show"
        });

        connection.SendTunneled(new ExecuteScriptWithStringParamsPacket
        {
            Script = "GameDock.OnUserEvent",
            Params =
        {
            "refreshConsumables",
            string.Empty,
            string.Empty
        }
        });

        connection.SendTunneled(new ExecuteScriptPacket
        {
            Script = "QuickItem.PopulateSelectors"
        });

        logger.LogInformation(
            "{connection} requested quick-item carousel open after shop-style owned item replay. ( Slot: {slot} )",
            connection,
            slot);
    }

    private static void SendGameDockConsumableEvent(
        GatewayConnection connection,
        string eventName,
        params string[] args)
    {
        var eventParams = new List<string> { eventName };
        eventParams.AddRange(args);

        connection.SendTunneled(new ExecuteScriptWithStringParamsPacket
        {
            Script = "GameDock.OnUserEvent",
            Params = eventParams
        });

        connection.SendTunneled(new ExecuteScriptWithStringParamsPacket
        {
            Script = "GameDock.OnUserEvent",
            Params = PadGameDockEventParams(eventName, args)
        });

        connection.SendTunneled(new ExecuteScriptWithStringParamsPacket
        {
            Script = $"GameDock.OnUserEvent,{eventName}"
        });

        connection.SendTunneled(new ExecuteScriptWithStringParamsPacket
        {
            Script = "Main_wndGameDock_swfGameDock.OnUserEvent",
            Params = eventParams
        });

        connection.SendTunneled(new ExecuteScriptWithStringParamsPacket
        {
            Script = "Main_wndGameDock_swfGameDock.OnUserEvent",
            Params = PadGameDockEventParams(eventName, args)
        });

        connection.SendTunneled(new ExecuteScriptWithStringParamsPacket
        {
            Script = "Main_wndGameDock_swfGameDock_OnUserEvent",
            Params = eventParams
        });

        connection.SendTunneled(new ExecuteScriptWithStringParamsPacket
        {
            Script = "Main_wndGameDock_swfGameDock_OnUserEvent",
            Params = PadGameDockEventParams(eventName, args)
        });
    }

    private static List<string> PadGameDockEventParams(string eventName, params string[] args)
    {
        var values = new List<string> { eventName };
        values.AddRange(args);

        while (values.Count < 3)
            values.Add(string.Empty);

        return values;
    }

    private static bool TryActivateBoombox(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ClientItem item,
        ClientItemDefinition definition,
        ILogger logger)
    {
        if (!IsBoomboxDefinition(definition))
            return false;

        var modelName = GetBoomboxModelName(definition);
        var textureAlias = GetBoomboxTextureAlias(definition);

        if (string.IsNullOrWhiteSpace(modelName))
        {
            logger.LogWarning(
                "{connection} activated boombox item with no model mapping. ( ItemGuid: {itemGuid}, Definition: {definition}, NameId: {nameId} )",
                connection,
                item.Id,
                item.Definition,
                definition.NameId);

            return true;
        }

        var modelDefinition = resourceManager.Models.Values
            .FirstOrDefault(x => string.Equals(x.ModelFileName, modelName, StringComparison.OrdinalIgnoreCase));

        if (modelDefinition is null)
        {
            logger.LogWarning(
                "{connection} activated boombox item but no model definition matched its asset. ( ItemGuid: {itemGuid}, Definition: {definition}, Asset: {asset} )",
                connection,
                item.Id,
                item.Definition,
                modelName);

            return true;
        }

        var presentation = GetBoomboxPresentation(definition, modelName, textureAlias);
        var boomboxKey = (connection.Player.Guid, item.Definition);
        var cooldownKey = (connection.Player.Guid, item.Definition);
        var now = DateTime.UtcNow;

        if (LastBoomboxSpawnByPlayerAndDefinition.TryGetValue(cooldownKey, out var lastSpawnedAt) &&
            now - lastSpawnedAt < BoomboxCooldown)
        {
            var remaining = BoomboxCooldown - (now - lastSpawnedAt);

            logger.LogInformation(
                "{connection} skipped boombox spawn due to cooldown. ( ItemGuid: {itemGuid}, Definition: {definition}, RemainingSeconds: {remainingSeconds} )",
                connection,
                item.Id,
                item.Definition,
                Math.Ceiling(remaining.TotalSeconds));

            return true;
        }

        DisposeActiveBoomboxesForPlayer(connection, logger);

        var spawnPosition = GetBoomboxSpawnPosition(connection);

        var npc = connection.Player.SpawnNpc(
            definition.NameId,
            modelDefinition.Id,
            modelDefinition.Scale,
            textureAlias,
            spawnPosition,
            connection.Player.Rotation,
            presentation.PrimaryCompositeEffectId,
            presentation.ObjectAnimationId);

        if (npc is null)
        {
            logger.LogWarning(
                "{connection} failed to spawn boombox object. ( ItemGuid: {itemGuid}, Definition: {definition}, TemplateId: {templateId}, Asset: {asset} )",
                connection,
                item.Id,
                item.Definition,
                modelDefinition.Id,
                modelName);

            return true;
        }

        LogBoomboxPresentation(connection, npc, presentation, logger);
        ActiveBoomboxes[boomboxKey] = new ActiveBoomboxState(npc.Guid, item.Definition, modelDefinition.Id, now);
        LastBoomboxSpawnByPlayerAndDefinition[cooldownKey] = now;
        QueueBoomboxDanceAnimation(connection, npc, presentation, logger);
        ScheduleBoomboxDespawn(npc, logger);

        logger.LogInformation(
            "{connection} spawned boombox. ( ItemGuid: {itemGuid}, Definition: {definition}, TemplateId: {templateId}, Asset: {asset}, TextureAlias: {textureAlias}, SpawnGuid: {spawnGuid}, Position: {position}, CompositeEffectId: {compositeEffectId}, LifetimeSeconds: {lifetimeSeconds}, CooldownSeconds: {cooldownSeconds}, CandidateAnimationAssets: {candidateAnimationAssets}, ObjectAnimationId: {objectAnimationId}, DanceAnimationId: {danceAnimationId} )",
            connection,
            item.Id,
            item.Definition,
            modelDefinition.Id,
            modelName,
            textureAlias,
            npc.Guid,
            npc.Position,
            npc.CompositeEffectId,
            BoomboxLifetime.TotalSeconds,
            BoomboxCooldown.TotalSeconds,
            string.Join(",", presentation.CandidateAnimationAssets),
            presentation.ObjectAnimationId,
            string.Join(",", presentation.DanceAnimationIds));

        return true;
    }

    private static void DisposeActiveBoomboxesForPlayer(GatewayConnection connection, ILogger logger)
    {
        foreach (var entry in ActiveBoomboxes.Where(x => x.Key.PlayerGuid == connection.Player.Guid).ToArray())
        {
            if (!ActiveBoomboxes.TryRemove(entry.Key, out var activeBoomboxState))
                continue;

            if (!connection.Player.Zone.TryGetNpc(activeBoomboxState.NpcGuid, out var activeBoombox))
                continue;

            logger.LogInformation(
                "{connection} despawning previous active boombox before spawning replacement. ( PlayerGuid: {playerGuid}, Definition: {definition}, SpawnGuid: {spawnGuid} )",
                connection,
                connection.Player.Guid,
                activeBoomboxState.DefinitionId,
                activeBoomboxState.NpcGuid);

            SendBoomboxRemove(activeBoombox);
            activeBoombox.Dispose();
        }
    }

    private static Vector4 GetBoomboxSpawnPosition(GatewayConnection connection)
    {
        var playerPosition = connection.Player.Position;
        var groundPosition = connection.Player.HasLastGroundedPosition
            ? connection.Player.LastGroundedPosition
            : playerPosition;
        var bestPosition = new Vector4(playerPosition.X, groundPosition.Y, playerPosition.Z, playerPosition.W);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var angle = Random.Shared.NextDouble() * Math.Tau;
            var radius = BoomboxSpawnMinRadius +
                (Random.Shared.NextSingle() * (BoomboxSpawnMaxRadius - BoomboxSpawnMinRadius));

            var candidate = new Vector4(
                playerPosition.X + (float)Math.Cos(angle) * radius,
                groundPosition.Y,
                playerPosition.Z + (float)Math.Sin(angle) * radius,
                playerPosition.W);

            bestPosition = candidate;

            var overlaps = ActiveBoomboxes.Values.Any(active =>
            {
                if (!connection.Player.Zone.TryGetNpc(active.NpcGuid, out var activeNpc))
                    return false;

                var dx = activeNpc.Position.X - candidate.X;
                var dz = activeNpc.Position.Z - candidate.Z;
                return MathF.Sqrt((dx * dx) + (dz * dz)) < BoomboxSpawnMinSpacing;
            });

            if (!overlaps)
                return candidate;
        }

        return bestPosition;
    }

    private static BoomboxPresentation GetBoomboxPresentation(
        ClientItemDefinition definition,
        string? modelName,
        string? textureAlias)
    {
        var fallbackToken = BoomboxDefinitionFallbacks.TryGetValue(definition.Id, out var fallback)
            ? fallback.Token
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(fallbackToken))
        {
            foreach (var (token, presentation) in BoomboxPresentations)
            {
                if (string.Equals(fallbackToken, token, StringComparison.OrdinalIgnoreCase))
                    return presentation;
            }
        }

        var key = $"{modelName} {textureAlias}";

        foreach (var (token, presentation) in BoomboxPresentations)
        {
            if (key.Contains(token, StringComparison.OrdinalIgnoreCase))
                return presentation;
        }

        return DefaultBoomboxPresentation;
    }

    private static bool IsBoomboxDefinition(ClientItemDefinition definition)
    {
        return BoomboxDefinitionFallbacks.ContainsKey(definition.Id) ||
            ContainsBoomboxToken(definition.ModelName) ||
            ContainsBoomboxToken(definition.TextureAlias);
    }

    private static string? GetBoomboxModelName(ClientItemDefinition definition)
    {
        if (BoomboxDefinitionFallbacks.TryGetValue(definition.Id, out var fallback))
            return fallback.ModelName;

        if (!string.IsNullOrWhiteSpace(definition.ModelName))
            return definition.ModelName;

        return null;
    }

    private static string? GetBoomboxTextureAlias(ClientItemDefinition definition)
    {
        if (BoomboxDefinitionFallbacks.TryGetValue(definition.Id, out var fallback))
            return fallback.TextureAlias;

        if (!string.IsNullOrWhiteSpace(definition.TextureAlias))
            return definition.TextureAlias;

        return null;
    }

    private static bool IsValidActionBarItem(ClientItemDefinition definition)
    {
        return definition.ActivatableAbilityId > 0 || IsBoomboxDefinition(definition);
    }

    private static bool ContainsBoomboxToken(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Contains("boombox", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogBoomboxPresentation(GatewayConnection connection, Npc npc, BoomboxPresentation presentation, ILogger logger)
    {
        if (npc.CompositeEffectId <= 0)
        {
            logger.LogInformation(
                "{connection} spawned boombox without a known composite effect. ( SpawnGuid: {spawnGuid}, ModelId: {modelId} )",
                connection,
                npc.Guid,
                npc.ModelId);

            return;
        }

        if (presentation.CandidateAnimationAssets.Length > 0)
        {
            logger.LogInformation(
                "{connection} boombox using attached composite effect and candidate ADR object animation assets. ( SpawnGuid: {spawnGuid}, ModelId: {modelId}, CompositeEffectId: {compositeEffectId}, ObjectAnimationId: {objectAnimationId}, CandidateAnimationAssets: {candidateAnimationAssets} )",
                connection,
                npc.Guid,
                npc.ModelId,
                npc.CompositeEffectId,
                presentation.ObjectAnimationId,
                string.Join(",", presentation.CandidateAnimationAssets));
        }
    }

    private static void ScheduleBoomboxDespawn(Npc npc, ILogger logger)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(BoomboxLifetime);

                if (!npc.Zone.TryGetNpc(npc.Guid, out _))
                    return;

                logger.LogInformation(
                    "Despawning boombox after lifetime expired. ( SpawnGuid: {spawnGuid}, ModelId: {modelId}, CompositeEffectId: {compositeEffectId}, LifetimeSeconds: {lifetimeSeconds} )",
                    npc.Guid,
                    npc.ModelId,
                    npc.CompositeEffectId,
                    BoomboxLifetime.TotalSeconds);

                SendBoomboxRemove(npc);
                npc.Dispose();

                foreach (var entry in ActiveBoomboxes)
                {
                    if (entry.Value.NpcGuid == npc.Guid)
                    {
                        ActiveBoomboxes.TryRemove(entry.Key, out _);

                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to despawn boombox after lifetime expired. ( SpawnGuid: {spawnGuid} )", npc.Guid);
            }
        });
    }

    private static void SendBoomboxRemove(Npc npc)
    {
        SendBoomboxCompositeEffectOverride(npc, 0);

        foreach (var player in npc.Zone.Players)
        {
            player.SendTunneled(new PlayerUpdatePacketRemovePlayerGracefully
            {
                Guid = npc.Guid,
                Animate = false,
                Delay = 0,
                EffectDelay = 0,
                CompositeEffectId = 0,
                Duration = 0
            });
        }
    }

    private static void SendBoomboxCompositeEffectOverride(Npc npc, int compositeEffectId)
    {
        foreach (var player in npc.Zone.Players.ToArray())
        {
            player.SendTunneled(new PlayerUpdatePacketSlotCompositeEffectOverride
            {
                Guid = npc.Guid,
                Slot = 0,
                CompositeEffect = compositeEffectId
            });
        }
    }

    private static void QueueBoomboxDanceAnimation(
        GatewayConnection connection,
        Npc npc,
        BoomboxPresentation presentation,
        ILogger logger)
    {
        if (presentation.DanceAnimationIds.Length == 0)
            return;

        QueueBoomboxDanceAnimationsForPlayersInRange(connection, npc, presentation.DanceAnimationIds, logger, "initial", true);
    }

    private static void QueueBoomboxDanceAnimationsForPlayersInRange(
        GatewayConnection connection,
        Npc npc,
        int[] danceAnimationIds,
        ILogger logger,
        string reason,
        bool interrupt)
    {
        var playersInRange = npc.Zone.Players
            .Where(player => IsWithinBoomboxDanceRange(player, npc))
            .ToArray();

        foreach (var player in playersInRange)
        {
            var danceAnimationId = SelectDanceAnimationId(player, danceAnimationIds);
            var sentCount = QueuePlayerDanceAnimation(player, playersInRange, danceAnimationId, interrupt);

            logger.LogInformation(
                "{connection} queued boombox dance animation for nearby player. ( Reason: {reason}, PlayerGuid: {playerGuid}, SpawnGuid: {spawnGuid}, DanceAnimationId: {danceAnimationId}, SentCount: {sentCount}, MaxDistance: {maxDistance} )",
                connection,
                reason,
                player.Guid,
                npc.Guid,
                danceAnimationId,
                sentCount,
                BoomboxDanceMaxDistance);
        }

        if (playersInRange.Length == 0)
        {
            logger.LogInformation(
                "{connection} skipped boombox dance tick because no players were in range. ( Reason: {reason}, SpawnGuid: {spawnGuid}, MaxDistance: {maxDistance} )",
                connection,
                reason,
                npc.Guid,
                BoomboxDanceMaxDistance);
        }
    }

    private static int SelectDanceAnimationId(Player player, int[] danceAnimationIds)
    {
        if (danceAnimationIds.Length == 1)
            return danceAnimationIds[0];

        var animationIndex = GetPlayerDanceAnimationIndex(player);
        if (animationIndex < danceAnimationIds.Length)
            return danceAnimationIds[animationIndex];

        return danceAnimationIds[(int)(player.Guid % (ulong)danceAnimationIds.Length)];
    }

    private static int GetPlayerDanceAnimationIndex(Player player)
    {
        var isFemale = player.Gender == 2;
        var isPixie = player.Model is 2 or 11 or 12 or 41 or 42 or 43 or 48 or 49 or 61;

        return (isPixie, isFemale) switch
        {
            (false, false) => 0,
            (false, true) => 1,
            (true, false) => 2,
            (true, true) => 3
        };
    }

    private static bool IsWithinBoomboxDanceRange(Player player, Npc npc)
    {
        var dx = player.Position.X - npc.Position.X;
        var dz = player.Position.Z - npc.Position.Z;

        return ((dx * dx) + (dz * dz)) <= BoomboxDanceMaxDistance * BoomboxDanceMaxDistance;
    }

    private static int QueuePlayerDanceAnimation(
        Player player,
        IReadOnlyCollection<Player> recipients,
        int danceAnimationId,
        bool interrupt)
    {
        var packet = new PlayerUpdatePacketQueueAnimation
        {
            Guid = player.Guid,
            AnimationId = danceAnimationId,
            Speed = 1.0f,
            Interrupt = interrupt
        };

        var sentPlayerGuids = new HashSet<ulong>();

        foreach (var recipient in recipients)
        {
            if (!sentPlayerGuids.Add(recipient.Guid))
                continue;

            recipient.SendTunneled(packet);
        }

        return sentPlayerGuids.Count;
    }

    private static void SetPlayerActionBarSlot(Player player, int slot, ActionBarSlot actionBarSlot)
    {
        var actionBar = EnsurePlayerActionBar(player);

        actionBar.Slots[slot] = actionBarSlot;
    }

    private static ClientActionBar EnsurePlayerActionBar(Player player)
    {
        if (!player.ActionBars.TryGetValue(ActionBarId, out var actionBar))
        {
            actionBar = new ClientActionBar
            {
                Id = ActionBarId
            };

            player.ActionBars[ActionBarId] = actionBar;
        }

        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (!actionBar.Slots.ContainsKey(slot))
                actionBar.Slots[slot] = CreateEmptySlot();
        }

        return actionBar;
    }

    private static int CreateCarouselAliasItemGuid(int itemGuid)
    {
        return CarouselAliasItemGuidBase + itemGuid;
    }

    private static bool IsCarouselAliasItemGuid(int itemGuid)
    {
        return itemGuid >= CarouselAliasItemGuidBase;
    }

    private static void SavePersistedSlot(
    GatewayConnection connection,
    IDbContextFactory<DatabaseContext> dbContextFactory,
    int slot,
    int itemGuid,
    ILogger logger)
    {
        try
        {
            var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);

            using var dbContext = dbContextFactory.CreateDbContext();
            using var connectionHandle = dbContext.Database.GetDbConnection();

            if (connectionHandle.State != ConnectionState.Open)
                connectionHandle.Open();

            EnsureItemActionBarSlotTable(connectionHandle);

            using var transaction = connectionHandle.BeginTransaction();

            using (var deleteCommand = connectionHandle.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = """
                    DELETE FROM ItemActionBarSlots
                    WHERE CharacterId = @characterId
                      AND ActionBarId = @actionBarId
                      AND (Slot = @slot OR ItemId = @itemId)
                    """;

                AddParameter(deleteCommand, "@characterId", characterId);
                AddParameter(deleteCommand, "@actionBarId", ActionBarId);
                AddParameter(deleteCommand, "@slot", slot);
                AddParameter(deleteCommand, "@itemId", itemGuid);
                deleteCommand.ExecuteNonQuery();
            }

            using (var insertCommand = connectionHandle.CreateCommand())
            {
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = """
                    INSERT INTO ItemActionBarSlots (CharacterId, ActionBarId, Slot, ItemId, ItemCharacterId)
                    VALUES (@characterId, @actionBarId, @slot, @itemId, @itemCharacterId)
                    """;

                AddParameter(insertCommand, "@characterId", characterId);
                AddParameter(insertCommand, "@actionBarId", ActionBarId);
                AddParameter(insertCommand, "@slot", slot);
                AddParameter(insertCommand, "@itemId", itemGuid);
                AddParameter(insertCommand, "@itemCharacterId", characterId);
                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();

            logger.LogInformation(
                "{connection} saved persisted item action bar slot. ( Slot: {slot}, ItemGuid: {itemGuid} )",
                connection,
                slot,
                itemGuid);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{connection} failed to save persisted item action bar slot. ( Slot: {slot}, ItemGuid: {itemGuid} )",
                connection,
                slot,
                itemGuid);
        }
    }

    private static void RemovePersistedSlot(
    GatewayConnection connection,
    IDbContextFactory<DatabaseContext> dbContextFactory,
    int slot,
    ILogger logger)
    {
        try
        {
            var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);

            using var dbContext = dbContextFactory.CreateDbContext();
            using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = """
                DELETE FROM ItemActionBarSlots
                WHERE CharacterId = @characterId AND ActionBarId = @actionBarId AND Slot = @slot
                """;

            AddParameter(command, "@characterId", characterId);
            AddParameter(command, "@actionBarId", ActionBarId);
            AddParameter(command, "@slot", slot);

            if (command.Connection!.State != ConnectionState.Open)
                command.Connection.Open();

            EnsureItemActionBarSlotTable(command.Connection);

            var rows = command.ExecuteNonQuery();

            if (rows <= 0)
                return;

            logger.LogInformation(
                "{connection} removed persisted item action bar slot. ( Slot: {slot} )",
                connection,
                slot);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{connection} failed to remove persisted item action bar slot. ( Slot: {slot} )",
                connection,
                slot);
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void EnsureItemActionBarSlotTable(DbConnection connection)
    {
        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ItemActionBarSlots (
                CharacterId INTEGER NOT NULL,
                ActionBarId INTEGER NOT NULL,
                Slot INTEGER NOT NULL,
                ItemId INTEGER NOT NULL,
                ItemCharacterId INTEGER NOT NULL,
                PRIMARY KEY (CharacterId, ActionBarId, Slot)
            )
            """;

        command.ExecuteNonQuery();
    }

    private sealed record BoomboxDefinitionFallback(
        string ModelName,
        string TextureAlias,
        string Token);

    private sealed record ActiveBoomboxState(
        ulong NpcGuid,
        int DefinitionId,
        int ModelId,
        DateTime CreatedAtUtc);

    private sealed record BoomboxPresentation(
        int PrimaryCompositeEffectId,
        string[] CandidateAnimationAssets,
        int[] DanceAnimationIds = null!,
        int ObjectAnimationId = DefaultBoomboxObjectAnimationId);
}
