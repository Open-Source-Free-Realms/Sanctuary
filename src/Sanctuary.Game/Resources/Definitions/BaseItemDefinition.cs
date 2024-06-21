using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

public class BaseItemDefinition : ISerializableType
{
    public int Id;

    public int Type;

    public int NameId;
    public int DescriptionId;

    public IconData Icon = new();

    public int Unknown;
    public int Unknown2;

    public int ActivatableAbilityId;
    public int PassiveAbilityId;

    public int Cost;

    public int Class;

    public int MaxStackSize;

    public int ProfileOverride;

    public int Slot;

    public bool NoTrade;
    public bool SingleUse;

    public string ModelName = null!;

    public int GenderUsage;

    public string TextureAlias = null!;

    public int CategoryId;

    public bool MembersOnly;
    public bool NonMiniGame;

    public bool NoSale;

    public int WeaponTrailEffectId;
    public int CompositeEffectId;

    public int PowerRating;

    public int MinProfileRank;

    public int Rarity;

    public string TintAlias = null!;

    public bool IsTintable;

    public bool ForceDisablePreview;

    public int MemberDiscount;

    public int RaceSetId;

    public int VipRankRequired;

    public int ClientEquipReqSetId;

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