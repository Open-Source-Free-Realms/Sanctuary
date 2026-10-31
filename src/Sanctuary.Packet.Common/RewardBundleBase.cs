using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleBase : ISerializableType, IDeserializableType
{
    public const int NoIconId = -1;
    public const int NoNameId = -1;

    // This payload is shared by RewardBundlePacket and the reward embedded in client collection data.
    public bool HasOptionalData = true;
    public int Coins;
    public int Experience;
    public int Unknown2;
    public int Unknown3 = 3;
    public int Unknown4;
    public int Unknown5;
    public float Multiplier = 1.0f;
    public int Unknown6;
    public int Unknown7;

    public ulong SourceGuid;
    public ulong PlayerGuid;

    public int IconId;
    public int NameId;
    public List<RewardBundleEntryBase> Entries { get; } = [];

    public int DescriptionId;

    public static RewardBundleBase CreateEmpty()
    {
        return new RewardBundleBase
        {
            HasOptionalData = false,
            Unknown3 = default,
            IconId = NoIconId,
            NameId = NoNameId
        };
    }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(HasOptionalData);
        writer.Write(Coins);
        writer.Write(Experience);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Multiplier);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(SourceGuid);
        writer.Write(PlayerGuid);
        writer.Write(IconId);
        writer.Write(NameId);
        writer.Write(Entries.Count);

        foreach (var entry in Entries)
            entry.Serialize(writer, HasOptionalData);

        writer.Write(DescriptionId);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out HasOptionalData))
            return false;

        if (!reader.TryRead(out Coins))
            return false;

        if (!reader.TryRead(out Experience))
            return false;

        if (!reader.TryRead(out Unknown2))
            return false;

        if (!reader.TryRead(out Unknown3))
            return false;

        if (!reader.TryRead(out Unknown4))
            return false;

        if (!reader.TryRead(out Unknown5))
            return false;

        if (!reader.TryRead(out Multiplier))
            return false;

        if (!reader.TryRead(out Unknown6))
            return false;

        if (!reader.TryRead(out Unknown7))
            return false;

        if (!reader.TryRead(out SourceGuid))
            return false;

        if (!reader.TryRead(out PlayerGuid))
            return false;

        if (!reader.TryRead(out IconId))
            return false;

        if (!reader.TryRead(out NameId))
            return false;

        if (!reader.TryRead(out int count) || count < 0)
            return false;

        Entries.Clear();

        for (var i = 0; i < count; i++)
        {
            if (!reader.TryRead(out RewardBundleEntryType type))
                return false;

            RewardBundleEntryBase? entry = type switch
            {
                RewardBundleEntryType.Item => new RewardBundleEntryItem(),
                RewardBundleEntryType.Experience => new RewardBundleEntryExperience(),
                RewardBundleEntryType.QuestAdd => new RewardBundleEntryQuestAdd(),
                RewardBundleEntryType.ProfileAdd => new RewardBundleEntryProfileAdd(),
                RewardBundleEntryType.AbilityLine => new RewardBundleEntryAbilityLine(),
                RewardBundleEntryType.CollectionAdd => new RewardBundleEntryCollectionAdd(),
                RewardBundleEntryType.CollectionEntryAdd => new RewardBundleEntryCollectionEntryAdd(),
                RewardBundleEntryType.Token => new RewardBundleEntryToken(),
                RewardBundleEntryType.PetTrickExperience => new RewardBundleEntryPetTrickExperience(),
                RewardBundleEntryType.Recipe => new RewardBundleEntryRecipe(),
                RewardBundleEntryType.ZoneFlag => new RewardBundleEntryZoneFlag(),
                RewardBundleEntryType.CharacterFlag => new RewardBundleEntryCharacterFlag(),
                RewardBundleEntryType.WheelSpin => new RewardBundleEntryWheelSpin(),
                RewardBundleEntryType.Trophy => new RewardBundleEntryTrophy(),
                RewardBundleEntryType.ClientExitUrl => new RewardBundleEntryClientExitUrl(),
                RewardBundleEntryType.FactoryExperience => new RewardBundleEntryFactoryExperience(),
                RewardBundleEntryType.Chest => new RewardBundleEntryChest(),
                _ => null
            };

            if (entry is null || !entry.TryRead(ref reader, HasOptionalData))
                return false;

            Entries.Add(entry);
        }

        return reader.TryRead(out DescriptionId);
    }
}
