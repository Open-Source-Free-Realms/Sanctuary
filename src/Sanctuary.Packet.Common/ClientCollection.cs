using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class ClientCollection : ISerializableType
{
    public int CategoryId;
    public int Id;
    public int DescriptionId;
    public int Type;
    public int IconId;
    public int IconTintId;

    // Defaults retain the values observed in the captured Briarwood collection row.
    public int Unknown1 = 1;
    public bool IsVisible = true;
    public int HeaderMetadata;
    public int Unknown2;
    public int Unknown3;
    public int Unknown4 = 15;
    public int Unknown5;
    public int Unknown6;
    public float Unknown7 = 1f;
    public int Unknown8;
    public int Unknown9;
    public int Unknown10;
    public int Unknown11;

    public ulong PlayerGuid;

    public int RewardIconId = 3901;
    public int RewardCurrencyIconId = 2416;
    public int Unknown12 = 1;
    public int Unknown13 = 3;
    public bool Unknown14;
    public int RewardIconId2 = 3901;
    public int Unknown15;
    public int RewardCurrencyIconId2 = 2416;
    public int RewardPoints = 50;
    public int RewardMetadata;
    public int Unknown16;
    public int Unknown17;
    public int Unknown18;
    public bool Unknown19;
    public int Unknown20 = 5604;

    public List<ClientCollectionEntry> Entries = [];

    public void Serialize(PacketWriter writer)
    {
        writer.Write(CategoryId);
        writer.Write(Id);
        writer.Write(DescriptionId);
        writer.Write(Type);
        writer.Write(IconId);
        writer.Write(IconTintId);

        writer.Write(Unknown1);
        writer.Write(IsVisible);
        writer.Write(HeaderMetadata);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Unknown8);
        writer.Write(Unknown9);
        writer.Write(Unknown10);
        writer.Write(Unknown11);

        writer.Write(PlayerGuid);

        writer.Write(RewardIconId);
        writer.Write(RewardCurrencyIconId);
        writer.Write(Unknown12);
        writer.Write(Unknown13);
        writer.Write(Unknown14);
        writer.Write(RewardIconId2);
        writer.Write(Unknown15);
        writer.Write(RewardCurrencyIconId2);
        writer.Write(RewardPoints);
        writer.Write(RewardMetadata);
        writer.Write(Unknown16);
        writer.Write(Unknown17);
        writer.Write(Unknown18);
        writer.Write(Unknown19);
        writer.Write(Unknown20);

        writer.Write(Entries);
    }
}
