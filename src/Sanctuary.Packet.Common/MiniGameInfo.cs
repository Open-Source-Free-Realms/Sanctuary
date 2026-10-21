using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class MiniGameInfo : ISerializableType, IDeserializableType
{
    public int NameId;

    public int IconId;

    public int DescriptionId;

    public int Difficulty;

    public int ProfileType;

    public int Type;

    public bool MembersOnly;

    public RewardBundleInfo RewardBundle = new();

    public RewardBundleInfo MemberRewardBundle = new();

    public RewardBundleInfo PreviewRewardBundle = new();

    public List<AchievementObjectiveInfo> Objectives { get; } = [];

    public bool ShowStarCounter;

    public bool ShowStatusIcon;

    public bool ShowActionBar;

    public bool Unknown11;

    public bool ShowEndDialog;

    public string? Unknown13;

    public int Unknown14;

    public bool Unknown15;

    public int PreselectedGameId;

    public bool Unknown16;

    public bool Unknown17;

    public bool Unknown18;

    public bool Unknown19;

    public int Unknown20;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(NameId);

        writer.Write(IconId);

        writer.Write(DescriptionId);

        writer.Write(Difficulty);

        writer.Write(ProfileType);

        writer.Write(Type);

        writer.Write(MembersOnly);

        RewardBundle.Serialize(writer);

        MemberRewardBundle.Serialize(writer);

        PreviewRewardBundle.Serialize(writer);

        writer.Write(Objectives.Count);

        foreach (var achievementObjectiveInfo in Objectives)
        {
            achievementObjectiveInfo.Serialize(writer);
        }

        writer.Write(ShowStarCounter);

        writer.Write(ShowStatusIcon);

        writer.Write(ShowActionBar);

        writer.Write(Unknown11);

        writer.Write(ShowEndDialog);

        writer.Write(Unknown13);

        writer.Write(Unknown14);

        writer.Write(Unknown15);

        writer.Write(PreselectedGameId);

        writer.Write(Unknown16);

        writer.Write(Unknown17);

        writer.Write(Unknown18);

        writer.Write(Unknown19);

        writer.Write(Unknown20);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out NameId))
            return false;

        if (!reader.TryRead(out IconId))
            return false;

        if (!reader.TryRead(out DescriptionId))
            return false;

        if (!reader.TryRead(out Difficulty))
            return false;

        if (!reader.TryRead(out ProfileType))
            return false;

        if (!reader.TryRead(out Type))
            return false;

        if (!reader.TryRead(out MembersOnly))
            return false;

        if (!RewardBundle.TryRead(ref reader))
            return false;

        if (!MemberRewardBundle.TryRead(ref reader))
            return false;

        if (!PreviewRewardBundle.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out int objectivesCount) || objectivesCount < 0 || objectivesCount > reader.RemainingLength / 103)
            return false;

        Objectives.Clear();

        for (var i = 0; i < objectivesCount; i++)
        {
            var achievementObjectiveInfo = new AchievementObjectiveInfo();

            if (!achievementObjectiveInfo.TryRead(ref reader))
                return false;

            Objectives.Add(achievementObjectiveInfo);
        }

        if (!reader.TryRead(out ShowStarCounter))
            return false;

        if (!reader.TryRead(out ShowStatusIcon))
            return false;

        if (!reader.TryRead(out ShowActionBar))
            return false;

        if (!reader.TryRead(out Unknown11))
            return false;

        if (!reader.TryRead(out ShowEndDialog))
            return false;

        var unknown13Reader = reader;
        if (!unknown13Reader.TryRead(out int unknown13Length) || unknown13Length < 0 || unknown13Length > unknown13Reader.RemainingLength)
            return false;

        if (!reader.TryRead(out Unknown13))
            return false;

        if (!reader.TryRead(out Unknown14))
            return false;

        if (!reader.TryRead(out Unknown15))
            return false;

        if (!reader.TryRead(out PreselectedGameId))
            return false;

        if (!reader.TryRead(out Unknown16))
            return false;

        if (!reader.TryRead(out Unknown17))
            return false;

        if (!reader.TryRead(out Unknown18))
            return false;

        if (!reader.TryRead(out Unknown19))
            return false;

        if (!reader.TryRead(out Unknown20))
            return false;

        return true;
    }
}
