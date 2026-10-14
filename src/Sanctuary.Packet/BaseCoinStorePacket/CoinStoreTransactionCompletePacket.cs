using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class CoinStoreTransactionCompletePacket : BaseCoinStorePacket, ISerializablePacket
{
    public new const short OpCode = 6;

    // 1 - Success
    // 3 - InvalidItem
    // 4 - InvalidQuantity
    // 5 - InvalidMerchant
    // 6 - InvalidItemTint
    // 7 - BalanceNotEnoughForPurchase
    // 8 - ServiceUnavailable
    // 9 - RecipientNotFound
    // 10 - NotAMember
    // 11 - RecipientCanNotAcceptItem
    public CoinStoreTransactionResult Result;

    public int ItemGuid;

    public CoinStoreTransactionRecord TransactionRecord = new();

    public CoinStoreTransactionCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write((int)Result);

        writer.Write(ItemGuid);

        TransactionRecord.Serialize(writer);

        return writer.Buffer;
    }
}