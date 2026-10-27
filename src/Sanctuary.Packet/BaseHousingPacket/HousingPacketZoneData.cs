using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class HousingPacketZoneData : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 45;

    /// <summary>
    /// Turns on the client's housing UI (Housing:SetIsInInstance). Zoning resets it,
    /// so this packet has to be sent again once the housing zone has loaded.
    /// </summary>
    public bool IsInInstance;

    public bool Unknown;

    public int HeadSize;

    public PlayerHousingInstanceInfo InstanceInfo = new();

    public HousingPacketZoneData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(IsInInstance);
        writer.Write(Unknown);
        writer.Write(HeadSize);

        InstanceInfo.Serialize(writer);

        return writer.Buffer;
    }
}