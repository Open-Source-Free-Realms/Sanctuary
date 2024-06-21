using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Game.Resources.Definitions;

public class ClientItemDefinition : BaseItemDefinition
{
    public int ResellValue;

    // ModelId
    // Player Customization Equipment Slot (sub_BD5980)
    public int Param1;

    /// <summary>
    /// Used in PlayerCustomization (sub_BD5980)
    /// </summary>
    public int Param2;

    public int Unknown5;

    public Dictionary<int, ClientItemStatDefinition> Stats = new();

    public List<ItemDefinition.ItemAbilityEntry> Abilities = new();

    public ClientItemDefinition(BaseItemDefinition other)
    {
        Id = other.Id;
        Type = other.Type;
        NameId = other.NameId;
        DescriptionId = other.DescriptionId;
        Icon = other.Icon;
        Unknown = other.Unknown;
        Unknown2 = other.Unknown2;
        ActivatableAbilityId = other.ActivatableAbilityId;
        PassiveAbilityId = other.PassiveAbilityId;
        Cost = other.Cost;
        Class = other.Class;
        MaxStackSize = other.MaxStackSize;
        ProfileOverride = other.ProfileOverride;
        Slot = other.Slot;
        NoTrade = other.NoTrade;
        SingleUse = other.SingleUse;
        ModelName = other.ModelName;
        GenderUsage = other.GenderUsage;
        TextureAlias = other.TextureAlias;
        CategoryId = other.CategoryId;
        MembersOnly = other.MembersOnly;
        NonMiniGame = other.NonMiniGame;
        NoSale = other.NoSale;
        WeaponTrailEffectId = other.WeaponTrailEffectId;
        CompositeEffectId = other.CompositeEffectId;
        PowerRating = other.PowerRating;
        MinProfileRank = other.MinProfileRank;
        Rarity = other.Rarity;
        TintAlias = other.TintAlias;
        IsTintable = other.IsTintable;
        ForceDisablePreview = other.ForceDisablePreview;
        MemberDiscount = other.MemberDiscount;
        RaceSetId = other.RaceSetId;
        VipRankRequired = other.VipRankRequired;
        ClientEquipReqSetId = other.ClientEquipReqSetId;
    }

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(ResellValue);

        writer.Write(Param1);
        writer.Write(Param2);
        writer.Write(Unknown5);

        writer.Write(Stats);

        writer.Write(Abilities);
    }
}