using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ItemInstance : ItemRecord
{
    public int Id;
    public int Count;
    public int ConsumedCount;
    public int LastCastTime = int.MaxValue;

    public int AbilityCount;

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(Id);
        writer.Write(Count);
        writer.Write(ConsumedCount);
        writer.Write(LastCastTime);

        // TODO: Rental
        writer.Write(false);

        writer.Write(AbilityCount);
    }
}