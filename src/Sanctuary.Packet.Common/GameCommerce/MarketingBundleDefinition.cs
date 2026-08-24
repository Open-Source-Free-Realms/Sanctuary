using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common.GameCommerce;

public class MarketingBundleDefinition : ISerializableType
{
    public string? Comment { get; set; }

    public DateTimeOffset LaunchTime { get; set; }
    public DateTimeOffset ExpireTime { get; set; }

    public ImageDataDefinition Image { get; set; } = new();

    public string? TintGroup { get; set; }

    public List<Entry> Entries { get; set; } = [];

    public class Entry : ISerializableType
    {
        public int Quantity { get; set; }
        public int GameItemId { get; set; }
        public int MarketingItemId { get; set; }

        public string? ImageTintOverride { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Quantity);
            writer.Write(GameItemId);
            writer.Write(MarketingItemId);
            writer.Write(ImageTintOverride);
        }
    }

    public int Id { get; set; }

    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    public int Status { get; set; }

    public int CurrencyType { get; set; }

    public int Price { get; set; }

    public int AltCurrencyPrice { get; set; }
    public int AltPrice { get; set; }

    public int PartnerId { get; set; }

    public int Unknown { get; set; }

    public bool Unknown2 { get; set; }

    public bool IsTintable { get; set; }

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(NameId);
        writer.Write(DescriptionId);

        writer.Write(Status);

        Image.Serialize(writer);

        writer.Write(IsTintable);
        writer.Write(TintGroup);

        writer.Write(CurrencyType);

        writer.Write(Price);

        writer.Write(AltCurrencyPrice);
        writer.Write(AltPrice);

        writer.Write(PartnerId);

        writer.Write(LaunchTime);
        writer.Write(ExpireTime);

        writer.Write(Unknown);
        writer.Write(Unknown2);

        writer.Write(LaunchTime);
        writer.Write(ExpireTime);

        writer.Write(Entries);
    }
}
