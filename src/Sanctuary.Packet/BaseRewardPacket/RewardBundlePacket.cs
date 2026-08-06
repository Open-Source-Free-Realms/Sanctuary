using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RewardBundlePacket : BaseRewardPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    // The coin shop provided the original item-entry capture. The entry list and item
    // subtype shape were recovered from the client's reward bundle implementation.
    public bool Success = true;
    public int Unknown1;
    public int RewardKind;
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
    public List<RewardBundleEntry> Entries { get; } = [];
    public int Unknown8;

    public RewardBundlePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Success);
        writer.Write(Unknown1);
        writer.Write(RewardKind);
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
            entry.Serialize(writer);

        writer.Write(Unknown8);

        return writer.Buffer;
    }
}
