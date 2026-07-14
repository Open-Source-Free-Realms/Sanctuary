using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

// CoinStoreMerchantListPacket (base 165 / sub 10): server→client, opens the merchant/shop
// window bound to a specific vendor NPC and populates its wares. Sent in response to the
// "Merchant" interaction (ShopInteraction, Type 17) being selected on a merchant NPC.
//
// Layout recovered from live client↔server captures (p2/p12 pcaps): the header
// [MerchantSetId][PlayerGuid][MerchantGuid] is confirmed — MerchantGuid == the vendor NPC
// guid, which the client then echoes back in every buy/sell (CoinStoreSellToClientRequest
// MerchantGuid + MerchantUnknown=MerchantSetId), so the existing buy/sell handlers work
// unchanged. The item list reuses the proven ItemDefinitionMetaData dictionary encoding
// (same as CoinStoreItemListPacket 165/1). The single `Unknown` int between the header and
// the list was 90184 in the capture; its meaning is unconfirmed (0 pending live check).
public class CoinStoreMerchantListPacket : BaseCoinStorePacket, ISerializablePacket
{
    public new const short OpCode = 10;

    public int MerchantSetId;
    public ulong PlayerGuid;
    public ulong MerchantGuid;

    // Observed constant 90184 (0x00016048) in the live merchant capture; purpose unconfirmed
    // but sending the observed value is faithful (sending 0 still opened the window).
    public int Unknown = 90184;

    // The merchant grid, as FIXED 38-byte records (NOT the 19-byte ItemDefinitionMetaData
    // dictionary the coin-store item lists use). PacketWriter.Write<T>(IList<T>) emits
    // [int count][each record] — matching the wire capture exactly.
    public List<CoinStoreMerchantItem> Items = new();

    public CoinStoreMerchantListPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // 165, 10

        writer.Write(MerchantSetId);
        writer.Write(PlayerGuid);
        writer.Write(MerchantGuid);
        writer.Write(Unknown);
        writer.Write(Items); // count + (key + ItemDefinitionMetaData) per entry

        return writer.Buffer;
    }
}
