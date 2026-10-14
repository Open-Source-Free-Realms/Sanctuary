using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class MerchantList : ISerializableType
{
    public int Unknown;

    public long Unknown2;

    public ulong NpcGuid;

    public int Unknown4;

    public List<Entry> Entries = [];

    public class Entry : ISerializableType
    {
        public int ItemDefinitionId;
        public int Unknown8;
        public int IconId;
        public int TintId;
        public int NameId;
        public int DescriptionId;
        public int PurchasableQty;

        public bool MembersOnly;

        public int Unknown9;
        public int AvailableTintGroupId;

        public bool CanBuy;

        public void Serialize(PacketWriter writer)
        {
            writer.Write(ItemDefinitionId);
            writer.Write(IconId);
            writer.Write(TintId);
            writer.Write(NameId);
            writer.Write(DescriptionId);
            writer.Write(PurchasableQty);
            writer.Write(MembersOnly);
            writer.Write(Unknown8);
            writer.Write(Unknown9);
            writer.Write(AvailableTintGroupId);
            writer.Write(CanBuy);
        }
    }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(NpcGuid);
        writer.Write(Unknown4);
        writer.Write(Entries);
    }
}