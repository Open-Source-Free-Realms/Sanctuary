using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class CharacterData : IDeserializable<CharacterData>
{
    public string? FirstName;
    public string? LastName;

    public string? TemporaryFirstName;
    public string? TemporaryLastName;

    public int Model;
    public int Head;
    public int Hair;
    public int ModelCustomization;
    public int FacePaint;
    public int SkinTone;

    public int ItemHands;
    public int ItemFeet;
    public int ItemLegs;
    public int ItemChest;
    public int ItemHead;
    public int ItemShoulders;

    public int TintHands;
    public int TintFeet;
    public int TintLegs;
    public int TintChest;
    public int TintHead;
    public int TintShoulders;

    public int EyeColor;
    public int HairColor;

    public int Location;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CharacterData value)
    {
        value = new CharacterData();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out value.FirstName))
            return false;

        if (!reader.TryRead(out value.LastName))
            return false;

        if (!reader.TryRead(out value.TemporaryFirstName))
            return false;

        if (!reader.TryRead(out value.TemporaryLastName))
            return false;

        if (!reader.TryRead(out value.Model))
            return false;

        if (!reader.TryRead(out value.Head))
            return false;

        if (!reader.TryRead(out value.Hair))
            return false;

        if (!reader.TryRead(out value.ModelCustomization))
            return false;

        if (!reader.TryRead(out value.FacePaint))
            return false;

        if (!reader.TryRead(out value.SkinTone))
            return false;

        if (!reader.TryRead(out value.ItemHands))
            return false;

        if (!reader.TryRead(out value.ItemFeet))
            return false;

        if (!reader.TryRead(out value.ItemLegs))
            return false;

        if (!reader.TryRead(out value.ItemChest))
            return false;

        if (!reader.TryRead(out value.ItemHead))
            return false;

        if (!reader.TryRead(out value.ItemShoulders))
            return false;

        if (!reader.TryRead(out value.TintHands))
            return false;

        if (!reader.TryRead(out value.TintFeet))
            return false;

        if (!reader.TryRead(out value.TintLegs))
            return false;

        if (!reader.TryRead(out value.TintChest))
            return false;

        if (!reader.TryRead(out value.TintHead))
            return false;

        if (!reader.TryRead(out value.TintShoulders))
            return false;

        if (!reader.TryRead(out value.EyeColor))
            return false;

        if (!reader.TryRead(out value.HairColor))
            return false;

        if (!reader.TryRead(out value.Location))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"{nameof(FirstName)}: \"{FirstName}\", " +
               $"{nameof(LastName)}: \"{LastName}\", " +
               $"{nameof(TemporaryFirstName)}: \"{TemporaryFirstName}\", " +
               $"{nameof(TemporaryLastName)}: \"{TemporaryLastName}\", " +
               $"{nameof(Model)}: {Model}, " +
               $"{nameof(Head)}: {Head}, " +
               $"{nameof(Hair)}: {Hair}, " +
               $"{nameof(ModelCustomization)}: {ModelCustomization}, " +
               $"{nameof(FacePaint)}: {FacePaint}, " +
               $"{nameof(SkinTone)}: {SkinTone}, " +
               $"{nameof(ItemHands)}: {ItemHands}, " +
               $"{nameof(ItemFeet)}: {ItemFeet}, " +
               $"{nameof(ItemLegs)}: {ItemLegs}, " +
               $"{nameof(ItemChest)}: {ItemChest}, " +
               $"{nameof(ItemHead)}: {ItemHead}, " +
               $"{nameof(ItemShoulders)}: {ItemShoulders}, " +
               $"{nameof(TintHands)}: {TintHands}, " +
               $"{nameof(TintFeet)}: {TintFeet}, " +
               $"{nameof(TintLegs)}: {TintLegs}, " +
               $"{nameof(TintChest)}: {TintChest}, " +
               $"{nameof(TintHead)}: {TintHead}, " +
               $"{nameof(TintShoulders)}: {TintShoulders}, " +
               $"{nameof(EyeColor)}: {EyeColor}, " +
               $"{nameof(HairColor)}: {HairColor}, " +
               $"{nameof(Location)}: {Location}";
    }
}