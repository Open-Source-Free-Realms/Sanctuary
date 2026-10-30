using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class EncounterPacketEncounterComplete : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterPacketEncounterComplete>
{
    public new const short OpCode = 3;

    public bool Won;
    public int CompositeEffectId;
    public int EarnedStars;
    public int TimeRemaining;
    public RewardBundleBase RewardBundle = RewardBundleBase.CreateEmpty();
    public RewardBundleBase MemberRewardBundle = RewardBundleBase.CreateEmpty();
    public RewardBundleBase RewardBundle3 = RewardBundleBase.CreateEmpty();

    public EncounterPacketEncounterComplete() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Won);
        writer.Write(CompositeEffectId);
        writer.Write(EarnedStars);
        writer.Write(TimeRemaining);

        RewardBundle.Serialize(writer);
        MemberRewardBundle.Serialize(writer);
        RewardBundle3.Serialize(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterPacketEncounterComplete encounterPacketEncounterComplete)
    {
        encounterPacketEncounterComplete = new();

        var reader = new PacketReader(data);

        if (!encounterPacketEncounterComplete.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out encounterPacketEncounterComplete.Won))
            return false;

        if (!reader.TryRead(out encounterPacketEncounterComplete.CompositeEffectId))
            return false;

        if (!reader.TryRead(out encounterPacketEncounterComplete.EarnedStars))
            return false;

        if (!reader.TryRead(out encounterPacketEncounterComplete.TimeRemaining))
            return false;

        if (!encounterPacketEncounterComplete.RewardBundle.TryRead(ref reader))
            return false;

        if (!encounterPacketEncounterComplete.MemberRewardBundle.TryRead(ref reader))
            return false;

        if (!encounterPacketEncounterComplete.RewardBundle3.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
