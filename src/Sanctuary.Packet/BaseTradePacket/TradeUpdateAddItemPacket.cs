using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public sealed class TradeUpdateAddItemPacket : BaseTradePacket
{
    protected override byte SubOpCode => 12;
    public int SessionId;
    public int Revision;
    public bool IsMine;
    public ItemInstance Item = new();

    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(SessionId);
        writer.Write(Revision);
        writer.Write(IsMine);
        // Copy into the exact wire type: ClientItem.Serialize adds an incompatible byte.
        new ItemInstance
        {
            Definition = Item.Definition, Tint = Item.Tint, Id = Item.Id, Count = Item.Count,
            ConsumedCount = Item.ConsumedCount, LastCastTime = Item.LastCastTime, AbilityCount = Item.AbilityCount
        }.Serialize(writer);
    }
}
