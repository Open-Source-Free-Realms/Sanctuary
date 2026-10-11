using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleBase : ISerializableType
{
    // This payload is shared by RewardBundlePacket and the reward embedded in client collection data.
    public bool Success = true;
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

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Success);
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
            entry.Serialize(writer);
    }
}
