using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class EncounterPacketEncounterComplete : BaseEncounterPacket, ISerializablePacket, IDeserializable<EncounterPacketEncounterComplete>
{
    public new const short OpCode = 3;

    public bool Unknown3;
    public int Unknown4;
    public int EarnedStars;
    public int TimeRemaining;
    public RewardBundleInfo RewardBundle = new();
    public RewardBundleInfo MemberRewardBundle = new();
    public RewardBundleInfo RewardBundle3 = new();

    public EncounterPacketEncounterComplete() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
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

        if (!reader.TryRead(out short opCode) || opCode != BaseEncounterPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != OpCode)
            return false;

        if (!reader.TryRead(out encounterPacketEncounterComplete.Unknown))
            return false;

        if (!reader.TryRead(out encounterPacketEncounterComplete.Unknown2))
            return false;

        if (!reader.TryRead(out encounterPacketEncounterComplete.Unknown3))
            return false;

        if (!reader.TryRead(out encounterPacketEncounterComplete.Unknown4))
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
