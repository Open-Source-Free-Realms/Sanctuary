using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class CoinStoreTransactionRecord : ISerializableType
{
    public CoinStoreTransactionType Type;

    public ulong MerchantGuid;

    public int Id;
    public DateTimeOffset Timestamp;

    public ItemRecord ItemRecord = new();

    public int Quantity;
    public int QuantityRemaining;

    public int Price;

    public void Serialize(PacketWriter writer)
    {
        writer.Write((int)Type);

        writer.Write(MerchantGuid);

        writer.Write(Id);
        writer.Write(Timestamp);

        ItemRecord.Serialize(writer);

        writer.Write(Quantity);

        writer.Write(Price);

        writer.Write(QuantityRemaining);
    }
}