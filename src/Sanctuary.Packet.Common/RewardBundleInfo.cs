using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleInfo : ISerializableType, IDeserializableType
{
    public bool HasOptionalData;
    public int Coins;
    public int Experience;
    public int Unknown4;
    public int Unknown5;
    public int Unknown6;
    public int Unknown7;
    public float Multiplier;
    public int Unknown9;
    public int Unknown10;
    public ulong SourceGuid;
    public ulong PlayerGuid;
    public int IconId;
    public int NameId;
    public List<RewardBundleEntryInfo> Entries { get; } = [];
    public int Unknown15;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(HasOptionalData);
        writer.Write(Coins);
        writer.Write(Experience);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Multiplier);
        writer.Write(Unknown9);
        writer.Write(Unknown10);
        writer.Write(SourceGuid);
        writer.Write(PlayerGuid);
        writer.Write(IconId);
        writer.Write(NameId);
        writer.Write(Entries.Count);

        foreach (var rewardBundleEntryInfo in Entries)
        {
            rewardBundleEntryInfo.HasOptionalData = HasOptionalData;
            rewardBundleEntryInfo.Serialize(writer);
        }

        writer.Write(Unknown15);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out HasOptionalData))
            return false;

        if (!reader.TryRead(out Coins))
            return false;

        if (!reader.TryRead(out Experience))
            return false;

        if (!reader.TryRead(out Unknown4))
            return false;

        if (!reader.TryRead(out Unknown5))
            return false;

        if (!reader.TryRead(out Unknown6))
            return false;

        if (!reader.TryRead(out Unknown7))
            return false;

        if (!reader.TryRead(out Multiplier))
            return false;

        if (!reader.TryRead(out Unknown9))
            return false;

        if (!reader.TryRead(out Unknown10))
            return false;

        if (!reader.TryRead(out SourceGuid))
            return false;

        if (!reader.TryRead(out PlayerGuid))
            return false;

        if (!reader.TryRead(out IconId))
            return false;

        if (!reader.TryRead(out NameId))
            return false;
        Entries.Clear();

        if (!reader.TryRead(out int count) || count < 0 || count > reader.RemainingLength / 38)
            return false;

        for (var i = 0; i < count; i++)
        {
            if (!reader.TryRead(out RewardBundleEntryType type))
                return false;

            RewardBundleEntryInfo? rewardBundleEntryInfo = type switch
            {
                RewardBundleEntryType.Item => new RewardBundleEntryItemInfo(),
                RewardBundleEntryType.Experience => new RewardBundleEntryExperienceInfo(),
                RewardBundleEntryType.QuestAdd => new RewardBundleEntryQuestAddInfo(),
                RewardBundleEntryType.ProfileAdd => new RewardBundleEntryProfileAddInfo(),
                RewardBundleEntryType.AbilityLine => new RewardBundleEntryAbilityLineInfo(),
                RewardBundleEntryType.CollectionAdd => new RewardBundleEntryCollectionAddInfo(),
                RewardBundleEntryType.CollectionEntryAdd => new RewardBundleEntryCollectionEntryAddInfo(),
                RewardBundleEntryType.Token => new RewardBundleEntryTokenInfo(),
                RewardBundleEntryType.PetTrickExperience => new RewardBundleEntryPetTrickExperienceInfo(),
                RewardBundleEntryType.Recipe => new RewardBundleEntryRecipeInfo(),
                RewardBundleEntryType.ZoneFlag => new RewardBundleEntryZoneFlagInfo(),
                RewardBundleEntryType.CharacterFlag => new RewardBundleEntryCharacterFlagInfo(),
                RewardBundleEntryType.WheelSpin => new RewardBundleEntryWheelSpinInfo(),
                RewardBundleEntryType.Trophy => new RewardBundleEntryTrophyInfo(),
                RewardBundleEntryType.ClientExitUrl => new RewardBundleEntryClientExitUrlInfo(),
                RewardBundleEntryType.FactoryExperience => new RewardBundleEntryFactoryExperienceInfo(),
                RewardBundleEntryType.Chest => new RewardBundleEntryChestInfo(),
                _ => null
            };

            if (rewardBundleEntryInfo is null)
                return false;

            rewardBundleEntryInfo.HasOptionalData = HasOptionalData;

            if (!rewardBundleEntryInfo.TryRead(ref reader))
                return false;

            Entries.Add(rewardBundleEntryInfo);
        }

        return reader.TryRead(out Unknown15);
    }
}
