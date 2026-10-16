using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PetClientInfo : ISerializableType
{
    public int Id;

    public byte Sex;

    public int IconId;

    public float Hunger;

    public float Hygiene;

    public float Play;

    public float Mood;

    public bool MembersOnly;

    public List<PetTrickInfo> PetTricks = new();

    public List<int> ItemGuids = new();

    public Dictionary<int, ProfileItemClassData> ItemClasses = new();

    public string Name = string.Empty;

    public int NameId;

    public int TintId;

    public string TextureAlias = string.Empty;

    public int ImageSetId;

    public bool IsVanityPet;

    public int AttachmentItemGroupId;

    public bool IsTutorialPet;

    public int EquippedPetTrick1;

    public int EquippedPetTrick2;

    public int EquippedPetTrick3;

    public int EquippedPetTrick4;

    public int UnknownItemGuid1;

    public int ShampooItemGuid;

    public int ConditionerItemGuid;

    public int ToyItemGuid;

    public int BrushItemGuid;

    public int FoodBowlItemGuid;

    public int FoodItemGuid;

    public int VitaminItemGuid;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Sex);
        writer.Write(IconId);
        writer.Write(Hunger);
        writer.Write(Hygiene);
        writer.Write(Play);
        writer.Write(Mood);
        writer.Write(MembersOnly);

        writer.Write(PetTricks.Count);

        foreach (var petTrickInfo in PetTricks)
        {
            writer.Write(petTrickInfo.Id);
            petTrickInfo.Serialize(writer);
        }

        writer.Write(ItemGuids.Count);

        foreach (var itemGuid in ItemGuids)
            writer.Write(itemGuid);

        writer.Write(ItemClasses.Count);

        foreach (var profileItemClassData in ItemClasses)
        {
            writer.Write(profileItemClassData.Key);
            profileItemClassData.Value.Serialize(writer);
        }

        writer.Write(Name);
        writer.Write(NameId);
        writer.Write(TintId);
        writer.Write(TextureAlias);
        writer.Write(ImageSetId);
        writer.Write(IsVanityPet);
        writer.Write(AttachmentItemGroupId);
        writer.Write(IsTutorialPet);
        writer.Write(EquippedPetTrick1);
        writer.Write(EquippedPetTrick2);
        writer.Write(EquippedPetTrick3);
        writer.Write(EquippedPetTrick4);
        writer.Write(UnknownItemGuid1);
        writer.Write(ShampooItemGuid);
        writer.Write(ConditionerItemGuid);
        writer.Write(ToyItemGuid);
        writer.Write(BrushItemGuid);
        writer.Write(FoodBowlItemGuid);
        writer.Write(FoodItemGuid);
        writer.Write(VitaminItemGuid);
    }
}
