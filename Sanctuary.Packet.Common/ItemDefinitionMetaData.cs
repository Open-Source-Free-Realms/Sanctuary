using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ItemDefinitionMetaData : ISerializableType
{
    public int Id;
    public int CategoryId;
    public int TintGroupId;
    public bool BuyDisabled;
    public bool CoinStoreOnly;
    public bool Hidden;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(CategoryId);
        writer.Write(TintGroupId);
        writer.Write(BuyDisabled);
        writer.Write(CoinStoreOnly);
        writer.Write(Hidden);
    }
}