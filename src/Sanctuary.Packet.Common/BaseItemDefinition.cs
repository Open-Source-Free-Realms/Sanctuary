using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class BaseItemDefinition : ISerializableType
{
    public int Id { get; set; }

    public int Type { get; set; }

    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    public IconData Icon { get; set; } = new();

    public int Unknown { get; set; }
    public int Unknown2 { get; set; }

    public int ActivatableAbilityId { get; set; }
    public int PassiveAbilityId { get; set; }

    public int Cost { get; set; }

    public int Class { get; set; }

    public int MaxStackSize { get; set; }

    public int ProfileOverride { get; set; }

    public int Slot { get; set; }

    public bool NoTrade { get; set; }
    public bool SingleUse { get; set; }

    public string ModelName { get; set; } = null!;

    public int GenderUsage { get; set; }

    public string TextureAlias { get; set; } = null!;

    public int CategoryId { get; set; }

    public bool MembersOnly { get; set; }
    public bool NonMiniGame { get; set; }

    public bool NoSale { get; set; }

    public int WeaponTrailEffectId { get; set; }
    public int CompositeEffectId { get; set; }

    public int PowerRating { get; set; }

    public int MinProfileRank { get; set; }

    public int Rarity { get; set; }

    public string TintAlias { get; set; } = null!;

    public bool IsTintable { get; set; }

    public bool ForceDisablePreview { get; set; }

    public int MemberDiscount { get; set; }

    public int RaceSetId { get; set; }

    public int VipRankRequired { get; set; }

    public int ClientEquipReqSetId { get; set; }

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(NameId);
        writer.Write(DescriptionId);

        Icon.Serialize(writer);

        writer.Write(Unknown);
        writer.Write(Unknown2);

        writer.Write(Cost);

        writer.Write(Class);

        writer.Write(ProfileOverride);

        writer.Write(Slot);

        writer.Write(NoTrade);
        writer.Write(NoSale);

        writer.Write(ModelName);
        writer.Write(TextureAlias);

        writer.Write(GenderUsage);

        writer.Write(Type);

        writer.Write(CategoryId);

        writer.Write(MembersOnly);
        writer.Write(NonMiniGame);

        writer.Write(WeaponTrailEffectId);
        writer.Write(CompositeEffectId);

        writer.Write(PowerRating);

        writer.Write(MinProfileRank);

        writer.Write(Rarity);

        writer.Write(ActivatableAbilityId);
        writer.Write(PassiveAbilityId);

        writer.Write(SingleUse);

        writer.Write(MaxStackSize);

        writer.Write(IsTintable);

        writer.Write(TintAlias);

        writer.Write(ForceDisablePreview);

        writer.Write(MemberDiscount);

        writer.Write(VipRankRequired);

        writer.Write(RaceSetId);

        writer.Write(ClientEquipReqSetId);
    }
}