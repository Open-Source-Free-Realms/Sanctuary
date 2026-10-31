using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class PendingHouseInvite : ISerializableType
{
    public ulong FromGuid;
    public ulong ToGuid;
    public ulong HouseGuid;

    public ulong Timestamp;

    public NameData FromName = new();
    public NameData ToName = new();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(FromGuid);
        writer.Write(ToGuid);
        writer.Write(HouseGuid);

        writer.Write(Timestamp);

        FromName.Serialize(writer);
        ToName.Serialize(writer);
    }
}