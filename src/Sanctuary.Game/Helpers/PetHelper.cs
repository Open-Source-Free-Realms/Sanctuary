using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class PetHelper
{
    private const int FirstTreasureSpringItemDefinitionId = 23427;
    private const int NoTint = 0;
    private const int LastTreasureSpringItemDefinitionId = 23458;
    private const float TreasureSpringChance = 0.1f;
    private const int TreasureRewardRollAttempts = 5;
    private const int TreasureChestOpenAnimationId = 1111;
    private const int TreasureChestOpenDurationMs = 3583;
    private const int TreasureChestOpenStandAnimationId = 1301;
    private const int SadThoughtBubbleIconId = 42;
    private const int SpeedBuffThoughtBubbleIconId = 155;
    private const int SpeedBuffLaunchCompositeEffectId = 15530;
    private const int EventThoughtBubbleSeconds = 15;
    private const int AmbientThoughtBubbleSeconds = 10;
    private const float NeedsSatisfiedMinNeed = 60f;
    private const int HungerThoughtBubbleIconId = 39;
    private const int HygieneThoughtBubbleIconId = 40;
    private const int PlayThoughtBubbleIconId = 41;
    private const float NeedHintMaxNeed = 25f;
    private const int TreasureThoughtBubbleIconId = 158;
    private const int CollectionThoughtBubbleIconId = 146;
    private const int ThoughtBubbleMinDelayMs = 45000;
    private const int ThoughtBubbleMaxDelayMs = 60000;
    private const int AnyGenderUsage = 0;
    private const int MaleModelGender = 1;
    private const byte MalePetSex = 1;
    private const byte FemalePetSex = 0;

    public const int NoItemGuid = 0;
    public const int MillisecondsPerSecond = 1000;
    public const float PetHappyMinMood = 25f;
    public const string PetDogModelPrefix = "dog_";
    public const string PetCatModelPrefix = "cat_";
    public const int PetShampooUtilitySlot = 1;
    public const int PetConditionerUtilitySlot = 2;
    public const int PetToyUtilitySlot = 3;
    public const int PetBrushUtilitySlot = 4;
    public const int PetFoodBowlUtilitySlot = 5;
    public const int PetFoodUtilitySlot = 6;
    public const int PetVitaminUtilitySlot = 7;
    public const int PetFirstUtilitySlot = PetShampooUtilitySlot;
    public const int PetLastUtilitySlot = PetVitaminUtilitySlot;
    public const int PetUtilitySlotOffset = 199;
    public const int PetFavoriteTrickSlot1 = 0;
    public const int PetFavoriteTrickSlot2 = 1;
    public const int PetFavoriteTrickSlot3 = 2;
    public const int PetFavoriteTrickSlot4 = 3;
    public const int PetFirstFavoriteTrickSlot = PetFavoriteTrickSlot1;
    public const int PetLastFavoriteTrickSlot = PetFavoriteTrickSlot4;

    public static readonly int[] PetClothingSlots = [1, 3, 5, 6];

    private static readonly (string ModelPrefix, int IconId)[] NoticeThoughtBubbles =
    [
        ("chugawug_", 43),
        ("cow", 44),
        ("dwarf_", 45),
        ("fairy_", 48),
        ("robgoblin_", 49),
        ("human_", 51),
        ("bixie_", 172),
        (PetCatModelPrefix, 174),
        (PetDogModelPrefix, 175),
        ("deer_", 176),
        ("forest_doe", 176),
        ("penguin_", 178),
        ("pig", 179),
        ("rabbit", 181),
        ("squirrel", 183),
        ("toad", 184),
        ("vulture", 185)
    ];

    public static DbPet CreatePet(int id, int definition, int tint) => new()
    {
        Id = id,
        Definition = definition,
        Tint = tint,
        Trait = (int)RollTrait()
    };

    public static void AddPet(Player player, IResourceManager resourceManager, DbPet dbPet, ClientItemDefinition clientItemDefinition,
        ModelDefinition modelDefinition)
    {
        player.Pets.Add(GetPetClientInfo(dbPet, clientItemDefinition, modelDefinition,
            resourceManager.Pets.TryGetValue(modelDefinition.Id, out var petDefinition) ? petDefinition : null));
        player.PetDefinitions.Add(dbPet.Id, dbPet.Definition);
        player.PetTraits.Add(dbPet.Id, dbPet.Trait == (int)PetTrait.None ? RollTrait() : (PetTrait)dbPet.Trait);

        if (dbPet.TreasureFoundAt is not null)
            player.PetTreasureFoundAt.Add(dbPet.Id, dbPet.TreasureFoundAt.Value);
    }

    public static bool TryGetDbPet(DatabaseContext dbContext, ulong characterId, int petId, [NotNullWhen(true)] out DbPet? dbPet)
    {
        dbPet = dbContext.Pets.SingleOrDefault(pet => pet.CharacterId == characterId && pet.Id == petId);
        return dbPet is not null;
    }

    public static bool TryGetUtilityItem(DatabaseContext dbContext, IResourceManager resourceManager, ulong characterId, PetClientInfo petClientInfo,
        int itemGuid, int slot, [NotNullWhen(true)] out DbItem? dbItem, [NotNullWhen(true)] out ClientItemDefinition? clientItemDefinition)
    {
        clientItemDefinition = null;
        dbItem = itemGuid == NoItemGuid ? null : dbContext.Items.SingleOrDefault(item => item.CharacterId == characterId
            && item.Id == itemGuid && item.Count > 0);

        if (dbItem is null || !resourceManager.ClientItemDefinitions.TryGetValue(dbItem.Definition, out var definition)
            || (definition.Type != ItemDefinitionHelper.PetUtilityItemType && definition.Type != ItemDefinitionHelper.PetClothingItemType)
            || definition.Slot != slot + PetUtilitySlotOffset
            || !petClientInfo.ItemClasses.ContainsKey(definition.Class))
        {
            return false;
        }

        clientItemDefinition = definition;
        return true;
    }

    public static bool TryGetPetActionItems(Player player, int petId, IDbContextFactory<DatabaseContext> dbContextFactory, IResourceManager resourceManager,
        int firstSlot, int secondSlot, [NotNullWhen(true)] out PetClientInfo? petClientInfo, [NotNullWhen(true)] out DbItem? firstItem,
        [NotNullWhen(true)] out ClientItemDefinition? firstDefinition, [NotNullWhen(true)] out ClientItemDefinition? secondDefinition)
    {
        firstItem = null;
        firstDefinition = null;
        secondDefinition = null;
        petClientInfo = player.GetActivePetClientInfo();

        if (player.Pet is null || petClientInfo is null || petId != player.ActivePetId)
        {
            player.PlayerPet.UnlockPetActions();
            return false;
        }

        var characterId = GuidHelper.GetPlayerId(player.Guid);
        using var dbContext = dbContextFactory.CreateDbContext();

        if (!TryGetUtilityItem(dbContext, resourceManager, characterId, petClientInfo, GetUtility(petClientInfo, firstSlot), firstSlot,
                out firstItem, out firstDefinition)
            || !TryGetUtilityItem(dbContext, resourceManager, characterId, petClientInfo, GetUtility(petClientInfo, secondSlot), secondSlot,
                out _, out secondDefinition))
        {
            player.PlayerPet.UnlockPetActions();
            return false;
        }

        return true;
    }

    public static bool CanWearClothing(PetClientInfo petClientInfo, ClientItemDefinition clientItemDefinition, int slot) =>
        clientItemDefinition.Type == ItemDefinitionHelper.PetClothingItemType && clientItemDefinition.Slot == slot
            && petClientInfo.ItemClasses.ContainsKey(clientItemDefinition.Class);

    public static void SavePetState(Player player, DbPet dbPet)
    {
        var petClientInfo = player.GetPetClientInfo(dbPet.Id);

        if (petClientInfo is null)
            return;

        dbPet.Hunger = petClientInfo.Hunger;
        dbPet.Hygiene = petClientInfo.Hygiene;
        dbPet.Play = petClientInfo.Play;
        dbPet.Mood = petClientInfo.Mood;
        dbPet.Trait = (int)player.PetTraits.GetValueOrDefault(dbPet.Id);

        if (player.PetTreasureFoundAt.TryGetValue(dbPet.Id, out var treasureFoundAt))
            dbPet.TreasureFoundAt = treasureFoundAt;
    }

    public static bool TryOpenTreasureChest(Player player, PetTreasureChest chest, IRewardManager rewardManager,
        IResourceManager resourceManager, out RewardDropDefinition? itemDrop)
    {
        itemDrop = null;

        if (!InteractionMenuHelper.CanInteract(chest, player) || !chest.IsOwner(player) || !chest.TryOpen())
            return false;

        EntityHelper.SetBaseAnimation(player, chest, TreasureChestOpenStandAnimationId);
        EntityHelper.SetAnimation(player, chest, TreasureChestOpenAnimationId);
        player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = chest.Guid,
            CompositeEffectId = chest.OpenCompositeEffectId,
            Position = chest.Position
        }, true);

        if (chest.CoinRewardTableKey is not null && rewardManager.TryRollReward(chest.CoinRewardTableKey, out var coinDrop) && coinDrop is not null)
            rewardManager.TryGrantReward(player, coinDrop, chest.Guid);

        itemDrop = RollTreasureSpring(player) ?? RollTreasureItem(player, chest, rewardManager, resourceManager);

        EntityHelper.RemovePlayerGracefully(chest, animate: true, delay: TreasureChestOpenDurationMs);
        return true;
    }

    private static ItemRewardDropDefinition? RollTreasureSpring(Player player)
    {
        if (Random.Shared.NextSingle() >= TreasureSpringChance)
            return null;

        var ownedItemDefinitionIds = player.Items.Select(item => item.Definition).ToHashSet();
        var springItemDefinitionIds = Enumerable.Range(FirstTreasureSpringItemDefinitionId,
                LastTreasureSpringItemDefinitionId - FirstTreasureSpringItemDefinitionId + 1)
            .Where(itemDefinitionId => !ownedItemDefinitionIds.Contains(itemDefinitionId))
            .ToArray();

        return springItemDefinitionIds.Length == 0 ? null : new ItemRewardDropDefinition
        {
            ItemDefinitionId = springItemDefinitionIds[Random.Shared.Next(springItemDefinitionIds.Length)]
        };
    }

    private static RewardDropDefinition? RollTreasureItem(Player player, PetTreasureChest chest, IRewardManager rewardManager,
        IResourceManager resourceManager)
    {
        for (var attempt = 0; attempt < TreasureRewardRollAttempts; attempt++)
        {
            if (!rewardManager.TryRollReward(chest.RewardTableKey, out var drop) || drop is null)
                return null;

            if (drop is ItemRewardDropDefinition itemDrop
                && resourceManager.ClientItemDefinitions.TryGetValue(itemDrop.ItemDefinitionId, out var itemDefinition)
                && itemDefinition.GenderUsage != AnyGenderUsage && itemDefinition.GenderUsage != player.Gender)
            {
                continue;
            }

            return drop;
        }

        return null;
    }

    private static PetTrait RollTrait() => (PetTrait)Random.Shared.Next((int)PetTrait.Friendly, (int)PetTrait.Excited + 1);

    private static int GetUtility(DbPet pet, int slot) => slot switch
    {
        PetShampooUtilitySlot => pet.ShampooItemId,
        PetConditionerUtilitySlot => pet.ConditionerItemId,
        PetToyUtilitySlot => pet.ToyItemId,
        PetBrushUtilitySlot => pet.BrushItemId,
        PetFoodBowlUtilitySlot => pet.FoodBowlItemId,
        PetFoodUtilitySlot => pet.FoodItemId,
        PetVitaminUtilitySlot => pet.VitaminItemId,
        _ => NoItemGuid
    };

    private static ref int GetUtility(PetClientInfo pet, int slot)
    {
        switch (slot)
        {
            case PetShampooUtilitySlot: return ref pet.ShampooItemGuid;
            case PetConditionerUtilitySlot: return ref pet.ConditionerItemGuid;
            case PetToyUtilitySlot: return ref pet.ToyItemGuid;
            case PetBrushUtilitySlot: return ref pet.BrushItemGuid;
            case PetFoodBowlUtilitySlot: return ref pet.FoodBowlItemGuid;
            case PetFoodUtilitySlot: return ref pet.FoodItemGuid;
            case PetVitaminUtilitySlot: return ref pet.VitaminItemGuid;
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    public static void RemoveItem(DbPet pet, int itemGuid)
    {
        if (itemGuid <= 0)
            return;

        for (var slot = PetFirstUtilitySlot; slot <= PetLastUtilitySlot; slot++)
        {
            if (GetUtility(pet, slot) == itemGuid)
                SetUtility(pet, slot, NoItemGuid);
        }
    }

    public static void RemoveItem(Player player, int itemGuid, int slot)
    {
        if (itemGuid <= 0)
            return;

        var changed = false;

        foreach (var ownedPet in player.Pets)
        {
            for (var utilitySlot = PetFirstUtilitySlot; utilitySlot <= PetLastUtilitySlot; utilitySlot++)
            {
                ref var utilityItemGuid = ref GetUtility(ownedPet, utilitySlot);

                if (utilityItemGuid != itemGuid)
                    continue;

                utilityItemGuid = NoItemGuid;
                changed = true;

                player.SendTunneled(new PetUtilityPacket
                {
                    Unequip = true,
                    PetId = ownedPet.Id,
                    Slot = utilitySlot
                });
            }

            if (ownedPet.ItemGuids.RemoveAll(equippedItemGuid => equippedItemGuid == itemGuid) == 0)
                continue;

            changed = true;

            if (ownedPet.Id == player.ActivePetId && player.Pet is not null
                && player.Pet.Guid == player.ActivePetGuid)
            {
                UnequipItem(player, player.Pet, slot);

                player.SendTunneled(new PetEquipPacket
                {
                    Unequip = true,
                    Slot = slot
                });
            }
        }

        if (changed)
            player.PlayerPet.SendPetList();
    }

    public static CharacterAttachmentData GetAttachment(int tint, ClientItemDefinition clientItemDefinition) => new()
    {
        ModelName = clientItemDefinition.ModelName,
        TextureAlias = clientItemDefinition.TextureAlias,
        TintAlias = clientItemDefinition.TintAlias,
        TintId = tint == NoTint ? clientItemDefinition.Icon.TintId : tint,
        CompositeEffectId = clientItemDefinition.CompositeEffectId,
        Slot = clientItemDefinition.Slot
    };

    public static void EquipItem(Player player, Npc npc, int itemGuid, CharacterAttachmentData attachment)
    {
        npc.Attachments.RemoveAll(equippedAttachment => equippedAttachment.Slot == attachment.Slot);

        if (itemGuid != NoItemGuid)
            npc.Attachments.Add(attachment);

        player.SendTunneledToVisible(new PlayerUpdatePacketEquipItemChange
        {
            Guid = npc.Guid,
            Id = itemGuid,
            Attachment = attachment
        }, true);
    }

    public static void UnequipItem(Player player, Npc npc, int slot)
    {
        EquipItem(player, npc, NoItemGuid, new CharacterAttachmentData
        {
            ModelName = string.Empty,
            TextureAlias = string.Empty,
            TintAlias = string.Empty,
            Slot = slot
        });
    }

    private static void SendThoughtBubble(Player player, Npc npc, int iconId, int seconds)
    {
        if (player.GetActivePetClientInfo() is not { IsVanityPet: false })
            return;

        EntityHelper.SendThoughtBubble(player, npc, iconId, seconds);
    }

    public static bool TrySendHappyThoughtBubble(Player player, Npc npc, PetClientInfo petClientInfo)
    {
        if (petClientInfo.Mood < PetHappyMinMood)
            return false;

        SendSpeedBuffThoughtBubble(player, npc);
        return true;
    }

    public static int GetThoughtBubbleDelayMs() => Random.Shared.Next(ThoughtBubbleMinDelayMs, ThoughtBubbleMaxDelayMs);

    public static float GetLowestNeed(PetClientInfo petClientInfo) =>
        Math.Min(petClientInfo.Hunger, Math.Min(petClientInfo.Hygiene, petClientInfo.Play));

    public static bool TrySendSummonThoughtBubble(Player player, Npc npc, PetClientInfo petClientInfo, bool speedBuffActive)
    {
        if (petClientInfo.Mood < PetHappyMinMood)
        {
            SendThoughtBubble(player, npc, SadThoughtBubbleIconId, EventThoughtBubbleSeconds);
            return true;
        }

        if (!speedBuffActive)
            return false;

        SendSpeedBuffThoughtBubble(player, npc);
        return true;
    }

    public static bool TrySendCareThoughtBubble(Player player, Npc npc, PetClientInfo petClientInfo, float previousLowestNeed)
    {
        var lowestNeed = GetLowestNeed(petClientInfo);

        if (previousLowestNeed < NeedsSatisfiedMinNeed && lowestNeed >= NeedsSatisfiedMinNeed)
        {
            SendSpeedBuffThoughtBubble(player, npc);
            return true;
        }

        if (lowestNeed < NeedHintMaxNeed)
        {
            var iconId = lowestNeed == petClientInfo.Hunger ? HungerThoughtBubbleIconId
                : lowestNeed == petClientInfo.Hygiene ? HygieneThoughtBubbleIconId
                : PlayThoughtBubbleIconId;

            SendThoughtBubble(player, npc, iconId, AmbientThoughtBubbleSeconds);
            return true;
        }

        if (petClientInfo.Mood >= PetHappyMinMood)
            return false;

        SendThoughtBubble(player, npc, SadThoughtBubbleIconId, AmbientThoughtBubbleSeconds);
        return true;
    }

    public static int GetNoticeThoughtBubbleIconId(ModelDefinition modelDefinition) =>
        NoticeThoughtBubbles.FirstOrDefault(noticeThoughtBubble =>
            modelDefinition.ModelFileName.StartsWith(noticeThoughtBubble.ModelPrefix, StringComparison.OrdinalIgnoreCase)).IconId;

    public static void SendNoticeThoughtBubble(Player player, Npc npc, int iconId) =>
        SendThoughtBubble(player, npc, iconId, AmbientThoughtBubbleSeconds);

    public static void SendTreasureThoughtBubble(Player player, Npc npc) =>
        SendThoughtBubble(player, npc, TreasureThoughtBubbleIconId, EventThoughtBubbleSeconds);

    public static void SendCollectionThoughtBubble(Player player, Npc npc) =>
        SendThoughtBubble(player, npc, CollectionThoughtBubbleIconId, AmbientThoughtBubbleSeconds);

    private static void SendSpeedBuffThoughtBubble(Player player, Npc npc)
    {
        SendThoughtBubble(player, npc, SpeedBuffThoughtBubbleIconId, EventThoughtBubbleSeconds);

        player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = npc.Guid,
            Unknown2 = player.Guid,
            CompositeEffectId = SpeedBuffLaunchCompositeEffectId,
            EffectDelay = EventThoughtBubbleSeconds * MillisecondsPerSecond,
            Position = npc.Position,
            Clear = true
        }, true);
    }

    public static void SetFavoriteTrick(DbPet dbPet, PetClientInfo petClientInfo, int slot, int trickId)
    {
        switch (slot)
        {
            case PetFavoriteTrickSlot1: dbPet.EquippedPetTrick1 = petClientInfo.EquippedPetTrick1 = trickId; break;
            case PetFavoriteTrickSlot2: dbPet.EquippedPetTrick2 = petClientInfo.EquippedPetTrick2 = trickId; break;
            case PetFavoriteTrickSlot3: dbPet.EquippedPetTrick3 = petClientInfo.EquippedPetTrick3 = trickId; break;
            case PetFavoriteTrickSlot4: dbPet.EquippedPetTrick4 = petClientInfo.EquippedPetTrick4 = trickId; break;
        }
    }

    public static void SetUtility(DbPet dbPet, PetClientInfo petClientInfo, int slot, int itemGuid)
    {
        SetUtility(dbPet, slot, itemGuid);
        GetUtility(petClientInfo, slot) = itemGuid;
    }

    private static void SetUtility(DbPet pet, int slot, int itemGuid)
    {
        switch (slot)
        {
            case PetShampooUtilitySlot: pet.ShampooItemId = itemGuid; break;
            case PetConditionerUtilitySlot: pet.ConditionerItemId = itemGuid; break;
            case PetToyUtilitySlot: pet.ToyItemId = itemGuid; break;
            case PetBrushUtilitySlot: pet.BrushItemId = itemGuid; break;
            case PetFoodBowlUtilitySlot: pet.FoodBowlItemId = itemGuid; break;
            case PetFoodUtilitySlot: pet.FoodItemId = itemGuid; break;
            case PetVitaminUtilitySlot: pet.VitaminItemId = itemGuid; break;
        }
    }


    public static bool TryGetModel(IResourceManager resourceManager, ClientItemDefinition clientItemDefinition,
        out ModelDefinition modelDefinition)
    {
        modelDefinition = null!;

        if (clientItemDefinition.Type != ItemDefinitionHelper.PetItemType)
            return false;

        modelDefinition = resourceManager.Models.Values.OrderBy(model => model.Id).FirstOrDefault(model =>
            string.Equals(model.ModelFileName, clientItemDefinition.ModelName, StringComparison.OrdinalIgnoreCase))!;

        return modelDefinition is not null;
    }

    private static PetClientInfo GetPetClientInfo(DbPet dbPet, ClientItemDefinition clientItemDefinition,
        ModelDefinition modelDefinition, PetDefinition? petDefinition)
    {
        var petClientInfo = new PetClientInfo
        {
            Id = dbPet.Id,
            EquippedPetTrick1 = dbPet.EquippedPetTrick1,
            EquippedPetTrick2 = dbPet.EquippedPetTrick2,
            EquippedPetTrick3 = dbPet.EquippedPetTrick3,
            EquippedPetTrick4 = dbPet.EquippedPetTrick4,

            ShampooItemGuid = dbPet.ShampooItemId,
            ConditionerItemGuid = dbPet.ConditionerItemId,
            ToyItemGuid = dbPet.ToyItemId,
            BrushItemGuid = dbPet.BrushItemId,
            FoodBowlItemGuid = dbPet.FoodBowlItemId,
            FoodItemGuid = dbPet.FoodItemId,
            VitaminItemGuid = dbPet.VitaminItemId,

            IconId = petDefinition?.IconId ?? 0,
            IsVanityPet = petDefinition?.IsVanityPet == true,
            Sex = modelDefinition.Gender == MaleModelGender ? MalePetSex : FemalePetSex,
            Hunger = dbPet.Hunger,
            Hygiene = dbPet.Hygiene,
            Play = dbPet.Play,
            Mood = dbPet.Mood,
            Name = dbPet.Name,
            NameId = clientItemDefinition.NameId,
            TintId = dbPet.Tint != NoTint ? dbPet.Tint : petDefinition?.DefaultTintId ?? NoTint,
            TextureAlias = clientItemDefinition.TextureAlias ?? string.Empty,
            ImageSetId = clientItemDefinition.Icon.Id,
            MembersOnly = clientItemDefinition.MembersOnly
        };

        petClientInfo.ItemGuids.AddRange(dbPet.Items.OrderBy(item => item.Id).Select(item => item.Id));

        if (petDefinition is not null)
        {
            petClientInfo.PetTricks.AddRange(petDefinition.PetTricks);

            foreach (var itemClass in petDefinition.ItemClasses)
                petClientInfo.ItemClasses.Add(itemClass.Key, itemClass.Value);
        }

        return petClientInfo;
    }
}
