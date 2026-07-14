using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

// One entry of a merchant/shop window's item grid, as carried by CoinStoreMerchantListPacket
// (base 165 / sub 10). This is a FIXED 38-byte record — NOT the 19-byte ItemDefinitionMetaData
// dictionary entry the coin-store item lists (165/1, 165/9) use. The layout was recovered
// byte-for-byte from a live merchant capture (merchant set 468, p12.pcap pkt#20016-20017) and
// cross-verified against ClientItemDefinitions.json.
//
// The client renders each item from its own local ClientItemDefinitions (it never asks the
// server for merchant-item definitions), and on buy it echoes field[0] (Id) back as the
// CoinStoreSellToClientRequest item definition — which the existing buy handler looks up and
// charges def.Cost. So only Id/IconId/NameId/DescriptionId need to be real; the rest are the
// display/flag constants observed on the wire.
public class CoinStoreMerchantItem : ISerializableType
{
    public int Id;            // item definition id; echoed by the client on buy (CONFIRMED)
    public int IconId;        // = ClientItemDefinition.Icon.Id (CONFIRMED)
    public int IconTintId;    // = ClientItemDefinition.Icon.TintId (0 in capture) (CONFIRMED)
    public int NameId;        // = ClientItemDefinition.NameId (CONFIRMED)
    public int DescriptionId; // = ClientItemDefinition.DescriptionId (CONFIRMED)
    public int Cost = -1;     // -1 sentinel in capture; server still charges def.Cost (value CONFIRMED, meaning inferred)
    public bool Unknown1;     // 0 in capture
    public int Quantity = 5;  // display stock per item (5 for most, 1000 for one); not enforced server-side
    public int Unknown2;      // 0 in capture
    public int Unknown3;      // 0 in capture
    public bool Unknown4 = true; // 1 in capture

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);           // [0:4]
        writer.Write(IconId);       // [4:8]
        writer.Write(IconTintId);   // [8:12]
        writer.Write(NameId);       // [12:16]
        writer.Write(DescriptionId);// [16:20]
        writer.Write(Cost);         // [20:24]
        writer.Write(Unknown1);     // [24]
        writer.Write(Quantity);     // [25:29]
        writer.Write(Unknown2);     // [29:33]
        writer.Write(Unknown3);     // [33:37]
        writer.Write(Unknown4);     // [37]  => 38 bytes total
    }
}
