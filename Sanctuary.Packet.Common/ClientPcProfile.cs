using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientPcProfile : ISerializableType
{
    public int Id;
    public int NameId;
    public int DescriptionId;
    public int Type;
    public int Icon;
    public int AbilityBgImageSet;
    public int BadgeImageSet;
    public int ButtonImageSet;
    public bool MembersOnly;
    public int IsCombat;

    public Dictionary<int, ProfileItemClassData> ItemClassData = new();

    public bool Unknown11;
    public int Unknown12;
    public int Unknown13;
    public bool Unknown14;
    public int Unknown15;
    public int Rank;
    public int RankPercent;
    public int StarsAvailable;
    public int StarsEarned;
    public int Unknown20;

    public Dictionary<int, ProfileItem> Items = new();

    public int Unknown21;

    public List<Ability> Abilities = new()
    {
        new Ability(),
        new Ability(),
        new Ability(),
        new Ability(),
        new Ability(),
        new Ability(),
        new Ability(),
        new Ability()
    };

    public List<AbilityExperience> AbilityExperiences = new()
    {
        new AbilityExperience()
    };

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Type);
        writer.Write(Icon);
        writer.Write(AbilityBgImageSet);
        writer.Write(BadgeImageSet);
        writer.Write(ButtonImageSet);
        writer.Write(MembersOnly);
        writer.Write(IsCombat);

        writer.Write(ItemClassData);

        writer.Write(Unknown11);
        writer.Write(Unknown12);
        writer.Write(Unknown13);
        writer.Write(Unknown14);
        writer.Write(Unknown15);
        writer.Write(Rank);
        writer.Write(RankPercent);
        writer.Write(StarsAvailable);
        writer.Write(StarsEarned);
        writer.Write(Unknown20);

        writer.Write(Items);

        writer.Write(Unknown21);

        writer.Write(Abilities);

        foreach (var abilityExperience in AbilityExperiences)
        {
            abilityExperience.Serialize(writer);

            if (abilityExperience.Unknown == 0)
                break;
        }
    }
}